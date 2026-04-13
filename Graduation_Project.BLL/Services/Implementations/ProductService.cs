using Graduation_Project.BLL.Common;
using Graduation_Project.BLL.DTOs.Product;
using Graduation_Project.BLL.Services.Interfaces;
using Graduation_Project.DAL.Models.Entities;
using Graduation_Project.DAL.Models.Enums;
using Graduation_Project.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Graduation_Project.BLL.Services.Implementations
{
    public class ProductService : IProductService
    {
        private readonly IUnitOfWork _unitOfWork;

        public ProductService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // Owner يضيف Product
        public async Task<ServiceResult<ProductDto>> CreateProductAsync(int userId, int brandId, CreateProductDto dto)
        {
            try
            {
                // تأكد إن الـ Brand بتاع الـ Owner ده
                var brand = await _unitOfWork.Brands
                    .GetQueryable()
                    .FirstOrDefaultAsync(b => b.BrandId == brandId && b.UserId == userId);

                if (brand == null)
                    return ServiceResult<ProductDto>.Failure("Brand not found or not yours");

                if (!brand.IsActive)
                    return ServiceResult<ProductDto>.Failure("Brand is not active");

                // تأكد إن الـ User وافق على العقد
                var user = await _unitOfWork.ApplicationUsers.GetByIdAsync(userId);
                if (user == null || !user.HasAcceptedContract)
                    return ServiceResult<ProductDto>.Failure("You must accept the contract first");

                // تأكد إن الـ Category موجودة
                var category = await _unitOfWork.Categories.GetByIdAsync(dto.CategoryId);
                if (category == null)
                    return ServiceResult<ProductDto>.Failure("Category not found");

                // لازم يكون فيه variants
                if (dto.Variants == null || !dto.Variants.Any())
                    return ServiceResult<ProductDto>.Failure("Product must have at least one variant");

                // لو بيقبل customization لازم يكون فيه zones
                if (dto.AllowsCustomization && (!dto.CustomizationZones.Any()))
                    return ServiceResult<ProductDto>.Failure("Customizable products must have at least one customization zone");

                var product = new Product
                {
                    BrandId = brandId,
                    CategoryId = dto.CategoryId,
                    ProductName = dto.ProductName,
                    Description = dto.Description,
                    ImageUrls = dto.ImageUrls,
                    AllowsCustomization = dto.AllowsCustomization,
                    ApprovalStatus = ApprovalStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = false // مش هيتنشر إلا بعد موافقة الـ AI
                };

                await _unitOfWork.Products.AddAsync(product);
                await _unitOfWork.SaveAsync();

                // إضافة الـ Variants
                foreach (var v in dto.Variants)
                {
                    await _unitOfWork.ProductVariants.AddAsync(new ProductVariant
                    {
                        ProductId = product.ProductId,
                        Size = v.Size,
                        Color = v.Color,
                        Price = v.Price,
                        StockQuantity = v.StockQuantity,
                        SKU = v.SKU ?? $"{product.ProductId}-{v.Size}-{v.Color}"
                    });
                }

                // إضافة الـ Customization Zones لو موجودة
                if (dto.AllowsCustomization)
                {
                    foreach (var zone in dto.CustomizationZones)
                    {
                        await _unitOfWork.ProductCustomizationZones.AddAsync(new ProductCustomizationZone
                        {
                            ProductId = product.ProductId,
                            Zone = (CustomizationZone)zone,
                            IsAvailable = true
                        });
                    }
                }

                await _unitOfWork.SaveAsync();

                return await GetProductByIdAsync(product.ProductId);
            }
            catch (Exception ex)
            {
                return ServiceResult<ProductDto>.Failure($"Error creating product: {ex.Message}");
            }
        }

        // يجيب Product بالـ ID
        public async Task<ServiceResult<ProductDto>> GetProductByIdAsync(int productId)
        {
            try
            {
                var product = await _unitOfWork.Products
                    .GetQueryable()
                    .Include(p => p.Brand)
                    .Include(p => p.Category)
                    .Include(p => p.Variants)
                    .Include(p => p.CustomizationZones)
                    .FirstOrDefaultAsync(p => p.ProductId == productId);

                if (product == null)
                    return ServiceResult<ProductDto>.Failure("Product not found");

                return ServiceResult<ProductDto>.Success(MapToDto(product));
            }
            catch (Exception ex)
            {
                return ServiceResult<ProductDto>.Failure($"Error fetching product: {ex.Message}");
            }
        }

        // كل الـ Products الـ Approved - للكاستمر
        public async Task<ServiceResult<List<ProductDto>>> GetAllApprovedProductsAsync()
        {
            try
            {
                var products = await _unitOfWork.Products
                    .GetQueryable()
                    .Include(p => p.Brand)
                    .Include(p => p.Category)
                    .Include(p => p.Variants)
                    .Include(p => p.CustomizationZones)
                    .Where(p => p.ApprovalStatus == ApprovalStatus.Approved && p.IsActive)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                return ServiceResult<List<ProductDto>>.Success(products.Select(MapToDto).ToList());
            }
            catch (Exception ex)
            {
                return ServiceResult<List<ProductDto>>.Failure($"Error fetching products: {ex.Message}");
            }
        }

        // Products بتاعت Brand معين
        public async Task<ServiceResult<List<ProductDto>>> GetProductsByBrandAsync(int brandId)
        {
            try
            {
                var products = await _unitOfWork.Products
                    .GetQueryable()
                    .Include(p => p.Brand)
                    .Include(p => p.Category)
                    .Include(p => p.Variants)
                    .Include(p => p.CustomizationZones)
                    .Where(p => p.BrandId == brandId && p.ApprovalStatus == ApprovalStatus.Approved && p.IsActive)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                return ServiceResult<List<ProductDto>>.Success(products.Select(MapToDto).ToList());
            }
            catch (Exception ex)
            {
                return ServiceResult<List<ProductDto>>.Failure($"Error fetching products: {ex.Message}");
            }
        }

        // Owner يشوف Products بتاعته كلها
        public async Task<ServiceResult<List<ProductDto>>> GetOwnerProductsAsync(int userId)
        {
            try
            {
                var products = await _unitOfWork.Products
                    .GetQueryable()
                    .Include(p => p.Brand)
                    .Include(p => p.Category)
                    .Include(p => p.Variants)
                    .Include(p => p.CustomizationZones)
                    .Where(p => p.Brand.UserId == userId)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                return ServiceResult<List<ProductDto>>.Success(products.Select(MapToDto).ToList());
            }
            catch (Exception ex)
            {
                return ServiceResult<List<ProductDto>>.Failure($"Error fetching products: {ex.Message}");
            }
        }

        // Admin يشوف الـ Pending Products
        public async Task<ServiceResult<List<ProductDto>>> GetPendingProductsAsync()
        {
            try
            {
                var products = await _unitOfWork.Products
                    .GetQueryable()
                    .Include(p => p.Brand)
                    .Include(p => p.Category)
                    .Include(p => p.Variants)
                    .Where(p => p.ApprovalStatus == ApprovalStatus.Pending)
                    .OrderBy(p => p.CreatedAt)
                    .ToListAsync();

                return ServiceResult<List<ProductDto>>.Success(products.Select(MapToDto).ToList());
            }
            catch (Exception ex)
            {
                return ServiceResult<List<ProductDto>>.Failure($"Error fetching pending products: {ex.Message}");
            }
        }

        // Owner يعدل Product بتاعته
        public async Task<ServiceResult<ProductDto>> UpdateProductAsync(int productId, int userId, UpdateProductDto dto)
        {
            try
            {
                var product = await _unitOfWork.Products
                    .GetQueryable()
                    .Include(p => p.Brand)
                    .Include(p => p.Variants)
                    .Include(p => p.CustomizationZones)
                    .FirstOrDefaultAsync(p => p.ProductId == productId);

                if (product == null)
                    return ServiceResult<ProductDto>.Failure("Product not found");

                if (product.Brand.UserId != userId)
                    return ServiceResult<ProductDto>.Failure("You can only update your own products");

                product.ProductName = dto.ProductName;
                product.Description = dto.Description;
                product.ImageUrls = dto.ImageUrls;
                product.CategoryId = dto.CategoryId;
                product.AllowsCustomization = dto.AllowsCustomization;
                product.IsActive = dto.IsActive;
                product.UpdatedAt = DateTime.UtcNow;

                // لما يتعدل يرجع Pending عشان الـ AI يراجعه تاني
                product.ApprovalStatus = ApprovalStatus.Pending;

                // حذف الـ Variants القديمة وإضافة الجديدة
                var oldVariants = await _unitOfWork.ProductVariants
                    .FindAsync(v => v.ProductId == productId);
                foreach (var v in oldVariants)
                    _unitOfWork.ProductVariants.Delete(v);

                foreach (var v in dto.Variants)
                {
                    await _unitOfWork.ProductVariants.AddAsync(new ProductVariant
                    {
                        ProductId = productId,
                        Size = v.Size,
                        Color = v.Color,
                        Price = v.Price,
                        StockQuantity = v.StockQuantity,
                        SKU = v.SKU ?? $"{productId}-{v.Size}-{v.Color}"
                    });
                }

                // تحديث الـ Customization Zones
                var oldZones = await _unitOfWork.ProductCustomizationZones
                    .FindAsync(z => z.ProductId == productId);
                foreach (var z in oldZones)
                    _unitOfWork.ProductCustomizationZones.Delete(z);

                if (dto.AllowsCustomization)
                {
                    foreach (var zone in dto.CustomizationZones)
                    {
                        await _unitOfWork.ProductCustomizationZones.AddAsync(new ProductCustomizationZone
                        {
                            ProductId = productId,
                            Zone = (CustomizationZone)zone,
                            IsAvailable = true
                        });
                    }
                }

                _unitOfWork.Products.Update(product);
                await _unitOfWork.SaveAsync();

                return await GetProductByIdAsync(productId);
            }
            catch (Exception ex)
            {
                return ServiceResult<ProductDto>.Failure($"Error updating product: {ex.Message}");
            }
        }

        // Owner يحذف Product بتاعته
        public async Task<ServiceResult<bool>> DeleteProductAsync(int productId, int userId)
        {
            try
            {
                var product = await _unitOfWork.Products
                    .GetQueryable()
                    .Include(p => p.Brand)
                    .FirstOrDefaultAsync(p => p.ProductId == productId);

                if (product == null)
                    return ServiceResult<bool>.Failure("Product not found");

                if (product.Brand.UserId != userId)
                    return ServiceResult<bool>.Failure("You can only delete your own products");

                _unitOfWork.Products.Delete(product);
                await _unitOfWork.SaveAsync();

                return ServiceResult<bool>.Success(true, "Product deleted successfully");
            }
            catch (Exception ex)
            {
                return ServiceResult<bool>.Failure($"Error deleting product: {ex.Message}");
            }
        }

        // AI يوافق أو يرفض
        public async Task<ServiceResult<ProductDto>> ApproveProductAsync(int productId, string aiResponse)
        {
            try
            {
                var product = await _unitOfWork.Products.GetByIdAsync(productId);
                if (product == null)
                    return ServiceResult<ProductDto>.Failure("Product not found");

                if (aiResponse == "approved")
                {
                    product.ApprovalStatus = ApprovalStatus.Approved;
                    product.ApprovalDate = DateTime.UtcNow;
                    product.IsActive = true;
                    product.RejectionReason = null;
                }
                else
                {
                    product.ApprovalStatus = ApprovalStatus.Rejected;
                    product.RejectionReason = aiResponse;
                    product.IsActive = false;
                }

                product.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.Products.Update(product);
                await _unitOfWork.SaveAsync();

                return await GetProductByIdAsync(productId);
            }
            catch (Exception ex)
            {
                return ServiceResult<ProductDto>.Failure($"Error approving product: {ex.Message}");
            }
        }

        // يجيب كل تقنيات الطباعة
        public async Task<ServiceResult<List<PrintingTechniqueDto>>> GetPrintingTechniquesAsync()
        {
            try
            {
                var techniques = await _unitOfWork.PrintingTechniques
                    .GetQueryable()
                    .Where(t => t.IsActive)
                    .ToListAsync();

                var result = techniques.Select(t => new PrintingTechniqueDto
                {
                    TechniqueId = t.TechniqueId,
                    Name = t.Name,
                    Dimensions = t.Dimensions,
                    Price = t.Price
                }).ToList();

                return ServiceResult<List<PrintingTechniqueDto>>.Success(result);
            }
            catch (Exception ex)
            {
                return ServiceResult<List<PrintingTechniqueDto>>.Failure($"Error fetching techniques: {ex.Message}");
            }
        }

        private ProductDto MapToDto(Product product) => new ProductDto
        {
            ProductId = product.ProductId,
            BrandId = product.BrandId,
            BrandName = product.Brand?.BrandName,
            CategoryId = product.CategoryId,
            CategoryName = product.Category?.CategoryName,
            ProductName = product.ProductName,
            Description = product.Description,
            ImageUrls = product.ImageUrls,
            AllowsCustomization = product.AllowsCustomization,
            CustomizationZones = product.CustomizationZones?
                .Select(z => z.Zone.ToString()).ToList() ?? new(),
            ApprovalStatus = product.ApprovalStatus,
            ApprovalStatusText = product.ApprovalStatus.ToString(),
            RejectionReason = product.RejectionReason,
            CreatedAt = product.CreatedAt,
            IsActive = product.IsActive,
            AverageRating = product.AverageRating,
            ReviewCount = product.ReviewCount,
            Variants = product.Variants?
                .Select(v => new ProductVariantDto
                {
                    VariantId = v.VariantId,
                    Size = v.Size,
                    Color = v.Color,
                    Price = v.Price,
                    StockQuantity = v.StockQuantity,
                    SKU = v.SKU
                }).ToList() ?? new()
        };
    }
}