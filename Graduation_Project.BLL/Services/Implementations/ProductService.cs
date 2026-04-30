using Graduation_Project.BLL.Common;
using Graduation_Project.BLL.DTOs.Product;
using Graduation_Project.BLL.Services.Interfaces;
using Graduation_Project.DAL.Models.Entities;
using Graduation_Project.DAL.Models.Enums;
using Graduation_Project.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace Graduation_Project.BLL.Services.Implementations
{
    public class ProductService : IProductService
    {
            private readonly IFileService _fileService;
        private readonly IUnitOfWork _unitOfWork;
            private readonly IAiModerationService _aiModerationService;


        public ProductService(IUnitOfWork unitOfWork,
    IAiModerationService aiModerationService,
    IFileService fileService)
{
    _unitOfWork = unitOfWork;
    _aiModerationService = aiModerationService;
    _fileService = fileService;
}
public async Task<ServiceResult<ProductDto>> CreateProductAsync(int userId, int brandId, CreateProductDto dto)
{
    try
    {
        // ================= BRAND =================
        var brand = await _unitOfWork.Brands
            .GetQueryable()
            .FirstOrDefaultAsync(b => b.BrandId == brandId && b.UserId == userId);

        if (brand == null)
            return ServiceResult<ProductDto>.Failure("Brand not found or not yours");

        if (!brand.IsActive)
            return ServiceResult<ProductDto>.Failure("Brand is not active");

        // ================= USER =================
        var user = await _unitOfWork.ApplicationUsers.GetByIdAsync(userId);
        if (user == null || !user.HasAcceptedContract)
            return ServiceResult<ProductDto>.Failure("You must accept the contract first");

        // ================= CATEGORY =================
        var category = await _unitOfWork.Categories.GetByIdAsync(dto.CategoryId);
        if (category == null)
            return ServiceResult<ProductDto>.Failure("Category not found");

        // ================= VARIANTS =================
        List<ProductVariantDto>? variants = null;

        if (!string.IsNullOrWhiteSpace(dto.VariantsJson))
        {
            variants = JsonSerializer.Deserialize<List<ProductVariantDto>>(
                dto.VariantsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
        }

        bool hasVariants = variants != null && variants.Any();

        // ================= VALIDATION =================
        if (!hasVariants)
        {
            if (dto.BasePrice is null || dto.BasePrice <= 0)
                return ServiceResult<ProductDto>.Failure("Base price is required");

            if (dto.StockQuantity is null || dto.StockQuantity <= 0)
                return ServiceResult<ProductDto>.Failure("Stock is required when no variants");
        }

        if (hasVariants && dto.StockQuantity != null)
            return ServiceResult<ProductDto>.Failure("Do not send stock when using variants");

        // ================= IMAGE UPLOAD =================
        List<string> uploadedUrls = new();

        if (dto.Images != null && dto.Images.Any())
        {
            foreach (var image in dto.Images)
            {
                var result = await _aiModerationService.ValidateImageAsync(image);

                if (!result.IsValid)
{
    return ServiceResult<ProductDto>.Success(
        null,
        result.Message ?? "Image rejected"
    );
}

                var url = await _fileService.UploadFileAsync(image, "images");
                uploadedUrls.Add(url);
            }
        }

        var imageUrl = uploadedUrls.FirstOrDefault();

        // ================= AI REQUEST =================
        var aiRequest = new AiModerationRequestDto
        {
            ProductName = dto.ProductName,
            Description = dto.Description,
            Category = category.CategoryName,
            Price = hasVariants ? variants!.Min(v => v.Price) : dto.BasePrice ?? 0,
            ImageUrl = imageUrl
        };

        // ================= MODERATION =================
        var aiResult = await _aiModerationService.ModerateProductAsync(aiRequest);

        if (aiResult.Status == "rejected")
        {
            return ServiceResult<ProductDto>.Failure(aiResult.Reason ?? "Rejected by AI");
        }

        var approvalStatus = aiResult.Status == "auto_approved"
            ? ApprovalStatus.Approved
            : ApprovalStatus.Pending;

        // ================= PRICE PREDICTION =================
        AiPredictionResultDto? aiPrediction = null;

      decimal finalPrice;

if (hasVariants)
{
    finalPrice = variants!.Min(v => v.Price);
}
else
{
    try
    {
        aiPrediction = await _aiModerationService.PredictProductAsync(
            aiRequest,
            imageUrl,
            brand.BrandName
        );

        var aiPrice = aiPrediction?.PricePrediction?.SuggestedPrice
                      ?? dto.BasePrice
                      ?? 0;

        // 🔥 هنا القرار
        if (dto.UseAiSuggestion)
            finalPrice = aiPrice;
        else
            finalPrice = dto.BasePrice ?? aiPrice; // fallback
    }
    catch
    {
        finalPrice = dto.BasePrice ?? 0;
    }
}

        // ================= PRODUCT =================
        var product = new Product
        {
            BrandId = brandId,
            CategoryId = dto.CategoryId,
            ProductName = dto.ProductName,
            Description = dto.Description,
            ImageUrls = string.Join(",", uploadedUrls),

            BasePrice = finalPrice,
            StockQuantity = hasVariants ? 0 : dto.StockQuantity!.Value,

            AllowsCustomization = dto.Customization != null,
            AllowsPrinting = dto.Customization?.AllowsPrinting ?? false,
            AllowsText = dto.Customization?.AllowsText ?? false,

            ApprovalStatus = approvalStatus,
            IsActive = approvalStatus == ApprovalStatus.Approved,

            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Products.AddAsync(product);
        await _unitOfWork.SaveAsync();

        // ================= DTO =================
        var productDto = await GetProductByIdAsync(product.ProductId);

        if (!productDto.Succeeded)
            return productDto;

        // ================= AI RESPONSE ATTACH =================
        if (aiPrediction?.PricePrediction != null)
        {
            productDto.Data.AiSuggestedPrice = aiPrediction.PricePrediction.SuggestedPrice;
            productDto.Data.MinPrice = aiPrediction.PricePrediction.PriceRange?.Min;
            productDto.Data.MaxPrice = aiPrediction.PricePrediction.PriceRange?.Max;
            productDto.Data.PriceReasoning = aiPrediction.PricePrediction.Reasoning;
        }

        return ServiceResult<ProductDto>.Success(
            productDto.Data,
            approvalStatus == ApprovalStatus.Approved
                ? "Product created successfully"
                : "Product pending review"
        );
    }
    catch (Exception ex)
    {
        return ServiceResult<ProductDto>.Failure(ex.Message);
    }
}// يجيب Product بالـ ID
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

        bool hasVariants = dto.Variants != null && dto.Variants.Any();

        // ======================================================
        // ✅ VALIDATION (same as create)
        // ======================================================
        if (!hasVariants)
        {
            if (dto.BasePrice <= 0)
                return ServiceResult<ProductDto>.Failure("Base price is required");

            if (dto.StockQuantity == null || dto.StockQuantity <= 0)
                return ServiceResult<ProductDto>.Failure("Stock is required when no variants");
        }

        if (hasVariants && dto.StockQuantity != null)
            return ServiceResult<ProductDto>.Failure("Do not send stock when using variants");

        bool allowsCustomization = dto.Customization != null &&
            (dto.Customization.AllowsPrinting || dto.Customization.AllowsText);

        if (allowsCustomization &&
            (dto.Customization.Zones == null || !dto.Customization.Zones.Any()))
        {
            return ServiceResult<ProductDto>.Failure("Customizable products must have at least one zone");
        }

        var category = await _unitOfWork.Categories.GetByIdAsync(dto.CategoryId);

        // ======================================================
        // 🖼️ IMAGE HANDLING (SMART MERGE)
        // ======================================================

        List<string> uploadedUrls = new();

        // 1️⃣ لو المستخدم رفع صور جديدة
        if (dto.Images != null && dto.Images.Any())
        {
            foreach (var img in dto.Images)
            {
                var url = await _fileService.UploadFileAsync(img, "products");
                uploadedUrls.Add(url);
            }
        }
        else
        {
            // 2️⃣ لو مفيش صور جديدة → احتفظ بالقديم
            if (!string.IsNullOrEmpty(product.ImageUrls))
                uploadedUrls = product.ImageUrls.Split(',').ToList();
        }

        // ======================================================
        // 🤖 AI MODERATION (use first image)
        // ======================================================
        var aiRequest = new AiModerationRequestDto
        {
            ProductName = dto.ProductName,
            Description = dto.Description,
            Price = hasVariants ? dto.Variants.Min(v => v.Price) : dto.BasePrice,
            Category = category?.CategoryName ?? "",
            ImageUrl = uploadedUrls.FirstOrDefault()
        };

        var aiResult = await _aiModerationService.ModerateProductAsync(aiRequest);

        var approvalStatus = aiResult.Status switch
        {
            "auto_approved" => ApprovalStatus.Approved,
            "rejected" => ApprovalStatus.Rejected,
            _ => ApprovalStatus.Pending
        };

        // ======================================================
        // 🧾 UPDATE CORE DATA
        // ======================================================
        product.ProductName = dto.ProductName;
        product.Description = dto.Description;
        product.CategoryId = dto.CategoryId;

        product.ImageUrls = string.Join(",", uploadedUrls);

        product.BasePrice = hasVariants
            ? dto.Variants.Min(v => v.Price)
            : dto.BasePrice;

        product.StockQuantity = hasVariants
            ? 0
            : dto.StockQuantity!.Value;

        product.AllowsCustomization = allowsCustomization;
        product.AllowsPrinting = allowsCustomization && dto.Customization?.AllowsPrinting == true;
        product.AllowsText = allowsCustomization && dto.Customization?.AllowsText == true;

        product.UpdatedAt = DateTime.UtcNow;

        product.ApprovalStatus = approvalStatus;
        product.IsActive = approvalStatus == ApprovalStatus.Approved;
        product.ApprovalDate = approvalStatus == ApprovalStatus.Approved ? DateTime.UtcNow : null;
        product.RejectionReason = approvalStatus == ApprovalStatus.Rejected ? aiResult.Reason : null;

        // ======================================================
        // 🔁 REPLACE VARIANTS
        // ======================================================
        var oldVariants = await _unitOfWork.ProductVariants.FindAsync(v => v.ProductId == productId);
        foreach (var v in oldVariants)
            _unitOfWork.ProductVariants.Delete(v);

        if (hasVariants)
        {
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
        }

        // ======================================================
        // 🎨 CUSTOMIZATION
        // ======================================================
        var oldZones = await _unitOfWork.ProductCustomizationZones.FindAsync(z => z.ProductId == productId);
        foreach (var z in oldZones)
            _unitOfWork.ProductCustomizationZones.Delete(z);

        if (allowsCustomization)
        {
            foreach (var zone in dto.Customization.Zones)
            {
                await _unitOfWork.ProductCustomizationZones.AddAsync(new ProductCustomizationZone
                {
                    ProductId = productId,
                    Zone = (CustomizationZone)zone,
                    IsAvailable = true
                });
            }
        }

        await _unitOfWork.SaveAsync();

        var productDto = await GetProductByIdAsync(productId);
        if (!productDto.Succeeded)
            return productDto;

        var message = approvalStatus switch
        {
            ApprovalStatus.Approved => "تم تحديث المنتج بنجاح",
            ApprovalStatus.Rejected => $"تم رفض التحديث: {aiResult.Reason}",
            _ => "تم تحديث المنتج وهو في انتظار المراجعة"
        };

        return ServiceResult<ProductDto>.Success(productDto.Data, message);
    }
    catch (Exception ex)
    {
        return ServiceResult<ProductDto>.Failure(ex.Message);
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

    CustomizationOptions = product.AllowsCustomization ? new ProductCustomizationOptionsDto
    {
        AllowsPrinting = product.AllowsPrinting,
        AllowsText = product.AllowsText,
        AvailableZones = product.CustomizationZones?
            .Where(z => z.IsAvailable)
            .Select(z => ((CustomizationZone)z.Zone).ToString())
            .ToList() ?? new()
    } : null,

    ApprovalStatus = product.ApprovalStatus,
    ApprovalStatusText = product.ApprovalStatus.ToString(),
    RejectionReason = product.RejectionReason,
    CreatedAt = product.CreatedAt,
    IsActive = product.IsActive,

    AverageRating = product.AverageRating,
    ReviewCount = product.ReviewCount,

    BasePrice = product.BasePrice,
    AiSuggestedPrice = null,
MinPrice = null,
MaxPrice = null,
PriceReasoning = null,

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

}}