using Graduation_Project.BLL.Common;
using Graduation_Project.BLL.DTOs.Order;
using Graduation_Project.BLL.Services.Interfaces;
using Graduation_Project.DAL.Models.Entities;
using Graduation_Project.DAL.Models.Enums;
using Graduation_Project.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Graduation_Project.BLL.Services.Implementations
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;

        public OrderService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

     public async Task<ServiceResult<OrderDto>> CreateOrderFromCartAsync(int userId, CreateOrderDto dto)
{
    using var transaction = await _unitOfWork.BeginTransactionAsync(); // 👈 لازم تضيفيها في UoW

    try
    {
        // 1️⃣ جيب الكارت
        var cartItems = await _unitOfWork.CartItems
            .GetQueryable()
            .Include(c => c.Product)
            .Include(c => c.ProductVariant)
            .Include(c => c.Technique)
            .Where(c => c.UserId == userId)
            .ToListAsync();

        if (!cartItems.Any())
            return ServiceResult<OrderDto>.Failure("Cart is empty");

        // 2️⃣ Validation + حساب الإجمالي
        decimal totalAmount = 0;

        foreach (var item in cartItems)
        {
            if (item.Product == null || !item.Product.IsActive)
                return ServiceResult<OrderDto>.Failure($"Product '{item.Product?.ProductName}' is no longer available");

            // ✅ تحديد السعر (variant أو base price)
            var unitPrice = item.ProductVariant?.Price ?? item.Product.BasePrice;
            var custPrice = item.Technique?.Price ?? 0;

            // ✅ التحقق من الـ Stock
            if (item.ProductVariant != null)
            {
                if (item.ProductVariant.StockQuantity < item.Quantity)
                    return ServiceResult<OrderDto>.Failure(
                        $"Not enough stock for '{item.Product.ProductName}' - only {item.ProductVariant.StockQuantity} available");
            }
            else
            {
                if (item.Product.StockQuantity < item.Quantity)
                    return ServiceResult<OrderDto>.Failure(
                        $"Not enough stock for '{item.Product.ProductName}' - only {item.Product.StockQuantity} available");
            }

            // ✅ الحساب الصح
            totalAmount += (unitPrice + custPrice) * item.Quantity;
        }

        // 3️⃣ إنشاء Order
        var order = new Order
        {
            UserId = userId,
            TotalAmount = totalAmount,
            OrderStatus = OrderStatus.Pending,
            ShippingAddress = dto.ShippingAddress,
            PaymentMethod = dto.PaymentMethod,
            PaymentStatus = PaymentStatus.Pending,
            TrackingNumber = GenerateTrackingNumber(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Orders.AddAsync(order);
        await _unitOfWork.SaveAsync(); // عشان ناخد OrderId

        // 4️⃣ إنشاء OrderItems
        var orderItems = new List<OrderItem>();

        foreach (var cartItem in cartItems)
        {
            var unitPrice = cartItem.ProductVariant?.Price ?? cartItem.Product.BasePrice;
            var custPrice = cartItem.Technique?.Price ?? 0;

            var orderItem = new OrderItem
            {
                OrderId = order.OrderId,
                ProductId = cartItem.ProductId,
                VariantId = cartItem.VariantId,
                Quantity = cartItem.Quantity,
                UnitPrice = unitPrice,
                CustomizationPrice = custPrice,
                Subtotal = (unitPrice + custPrice) * cartItem.Quantity // ✅ fix
            };

            orderItems.Add(orderItem);
            await _unitOfWork.OrderItems.AddAsync(orderItem);
        }

        await _unitOfWork.SaveAsync(); // حفظ كل الـ items مرة واحدة

        // 5️⃣ Customization
        foreach (var cartItem in cartItems)
        {
            if (cartItem.TechniqueId != null && cartItem.CustomizationZone != null)
            {
                var orderItem = orderItems.First(o =>
                    o.ProductId == cartItem.ProductId &&
                    o.VariantId == cartItem.VariantId);

                await _unitOfWork.OrderItemCustomizations.AddAsync(new OrderItemCustomization
                {
                    OrderItemId = orderItem.OrderItemId,
                    Zone = (CustomizationZone)cartItem.CustomizationZone.Value,
                    TechniqueId = cartItem.TechniqueId.Value,
                    DesignImageUrl = cartItem.DesignImageUrl,
                    DesignText = cartItem.DesignText,
                    CustomizationPrice = cartItem.Technique?.Price ?? 0
                });
            }
        }

        // 6️⃣ تحديث الـ Stock
        foreach (var cartItem in cartItems)
        {
            if (cartItem.ProductVariant != null)
            {
                cartItem.ProductVariant.StockQuantity -= cartItem.Quantity;
                _unitOfWork.ProductVariants.Update(cartItem.ProductVariant);
            }
            else
            {
                cartItem.Product.StockQuantity -= cartItem.Quantity;
                _unitOfWork.Products.Update(cartItem.Product);
            }
        }

        // 7️⃣ مسح الكارت
        foreach (var cartItem in cartItems)
            _unitOfWork.CartItems.Delete(cartItem);

        await _unitOfWork.SaveAsync();

        await transaction.CommitAsync();

        return await GetOrderByIdAsync(order.OrderId, userId);
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync();
        return ServiceResult<OrderDto>.Failure($"Error creating order: {ex.Message}");
    }
}

        public async Task<ServiceResult<OrderDto>> GetOrderByIdAsync(int orderId, int userId)
        {
            try
            {
                var order = await _unitOfWork.Orders
                    .GetQueryable()
                    .Include(o => o.User)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                            .ThenInclude(p => p.Brand)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.ProductVariant)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Customizations)
                            .ThenInclude(c => c.Technique)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId);

                if (order == null)
                    return ServiceResult<OrderDto>.Failure("Order not found");

                return ServiceResult<OrderDto>.Success(MapToDto(order));
            }
            catch (Exception ex)
            {
                return ServiceResult<OrderDto>.Failure($"Error fetching order: {ex.Message}");
            }
        }

        public async Task<ServiceResult<List<OrderDto>>> GetMyOrdersAsync(int userId)
        {
            try
            {
                var orders = await _unitOfWork.Orders
                    .GetQueryable()
                    .Include(o => o.User)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                            .ThenInclude(p => p.Brand)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.ProductVariant)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Customizations)
                            .ThenInclude(c => c.Technique)
                    .Where(o => o.UserId == userId)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                return ServiceResult<List<OrderDto>>.Success(orders.Select(MapToDto).ToList());
            }
            catch (Exception ex)
            {
                return ServiceResult<List<OrderDto>>.Failure($"Error fetching orders: {ex.Message}");
            }
        }

        public async Task<ServiceResult<List<OrderDto>>> GetAllOrdersAsync()
        {
            try
            {
                var orders = await _unitOfWork.Orders
                    .GetQueryable()
                    .Include(o => o.User)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Product)
                            .ThenInclude(p => p.Brand)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.ProductVariant)
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.Customizations)
                            .ThenInclude(c => c.Technique)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                return ServiceResult<List<OrderDto>>.Success(orders.Select(MapToDto).ToList());
            }
            catch (Exception ex)
            {
                return ServiceResult<List<OrderDto>>.Failure($"Error fetching orders: {ex.Message}");
            }
        }

        public async Task<ServiceResult<OrderDto>> UpdateOrderStatusAsync(int orderId, OrderStatus status)
        {
            try
            {
                var order = await _unitOfWork.Orders
                    .GetQueryable()
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null)
                    return ServiceResult<OrderDto>.Failure("Order not found");

                order.OrderStatus = status;
                order.UpdatedAt = DateTime.UtcNow;

                // لو الأوردر اتشحن يبقى Paid
                if (status == OrderStatus.Shipped)
                    order.PaymentStatus = PaymentStatus.Paid;

                _unitOfWork.Orders.Update(order);
                await _unitOfWork.SaveAsync();

                return await GetOrderByIdAsync(orderId, order.UserId);
            }
            catch (Exception ex)
            {
                return ServiceResult<OrderDto>.Failure($"Error updating order: {ex.Message}");
            }
        }

        public async Task<ServiceResult<OrderDto>> CancelOrderAsync(int orderId, int userId)
        {
            try
            {
                var order = await _unitOfWork.Orders
                    .GetQueryable()
                    .Include(o => o.OrderItems)
                        .ThenInclude(oi => oi.ProductVariant)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId && o.UserId == userId);

                if (order == null)
                    return ServiceResult<OrderDto>.Failure("Order not found");

                if (order.OrderStatus != OrderStatus.Pending)
                    return ServiceResult<OrderDto>.Failure("Only pending orders can be cancelled");

                order.OrderStatus = OrderStatus.Cancelled;
                order.UpdatedAt = DateTime.UtcNow;

                // رجّع الـ Stock
                foreach (var item in order.OrderItems)
                {
                    if (item.ProductVariant != null)
                    {
                        item.ProductVariant.StockQuantity += item.Quantity;
                        _unitOfWork.ProductVariants.Update(item.ProductVariant);
                    }
                }

                _unitOfWork.Orders.Update(order);
                await _unitOfWork.SaveAsync();

                return await GetOrderByIdAsync(orderId, userId);
            }
            catch (Exception ex)
            {
                return ServiceResult<OrderDto>.Failure($"Error cancelling order: {ex.Message}");
            }
        }

        private string GenerateTrackingNumber()
            => $"LB-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

        private OrderDto MapToDto(Order order) => new OrderDto
        {
            OrderId = order.OrderId,
            UserId = order.UserId,
            CustomerName = order.User?.FullName,
            TotalAmount = order.TotalAmount,
            OrderStatus = order.OrderStatus,
            OrderStatusText = order.OrderStatus.ToString(),
            ShippingAddress = order.ShippingAddress,
            PaymentMethod = order.PaymentMethod,
            PaymentMethodText = order.PaymentMethod.ToString(),
            PaymentStatus = order.PaymentStatus,
            PaymentStatusText = order.PaymentStatus.ToString(),
            TrackingNumber = order.TrackingNumber,
            CreatedAt = order.CreatedAt,
            Items = order.OrderItems?.Select(oi => new OrderItemDto
            {
                OrderItemId = oi.OrderItemId,
                ProductId = oi.ProductId,
                ProductName = oi.Product?.ProductName,
                ProductImage = oi.Product?.ImageUrls?.Split(',').FirstOrDefault()?.Trim(),
                BrandName = oi.Product?.Brand?.BrandName,
                VariantId = oi.VariantId,
                Size = oi.ProductVariant?.Size,
                Color = oi.ProductVariant?.Color,
                Quantity = oi.Quantity,
                UnitPrice = oi.UnitPrice,
                CustomizationPrice = oi.CustomizationPrice,
                Subtotal = oi.Subtotal,
                Customization = oi.Customizations?.FirstOrDefault() != null
                    ? new OrderItemCustomizationDto
                    {
                        Zone = oi.Customizations.First().Zone.ToString(),
                        TechniqueName = oi.Customizations.First().Technique?.Name,
                        DesignImageUrl = oi.Customizations.First().DesignImageUrl,
                        DesignText = oi.Customizations.First().DesignText,
                        TechniquePrice = oi.Customizations.First().Technique?.Price ?? 0
                    }
                    : null
            }).ToList() ?? new()
        };
    }
}