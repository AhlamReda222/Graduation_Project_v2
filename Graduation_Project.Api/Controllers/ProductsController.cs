using Graduation_Project.BLL.DTOs.Product;
using Graduation_Project.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Graduation_Project.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;
            private readonly IProductDescriptionService _descriptionService;

public ProductsController(
        IProductService productService,
        IProductDescriptionService descriptionService)
    {
        _productService = productService;
        _descriptionService = descriptionService;
    }

        // الكل يشوف المنتجات الـ Approved
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _productService.GetAllApprovedProductsAsync();
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok(result.Data);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _productService.GetProductByIdAsync(id);
            if (!result.Succeeded) return NotFound(result.Errors);
            return Ok(result.Data);
        }

        // Products بتاعت Brand معين
        [HttpGet("brand/{brandId}")]
        public async Task<IActionResult> GetByBrand(int brandId)
        {
            var result = await _productService.GetProductsByBrandAsync(brandId);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok(result.Data);
        }

        // تقنيات الطباعة - للكل
        [HttpGet("printing-techniques")]
        public async Task<IActionResult> GetPrintingTechniques()
        {
            var result = await _productService.GetPrintingTechniquesAsync();
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok(result.Data);
        }

        // Owner يشوف Products بتاعته
        [HttpGet("my-products")]
        [Authorize(Policy = "BrandOwnerOnly")]
        public async Task<IActionResult> GetMyProducts()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _productService.GetOwnerProductsAsync(userId);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok(result.Data);
        }

        // Admin يشوف الـ Pending
        [HttpGet("pending")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetPending()
        {
            var result = await _productService.GetPendingProductsAsync();
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok(result.Data);
        }

        // Owner يضيف Product
     [HttpPost("brand/{brandId}")]
[Authorize(Policy = "BrandOwnerOnly")]
public async Task<IActionResult> Create(int brandId, [FromForm] CreateProductDto dto)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);

   var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    var result = await _productService.CreateProductAsync(userId, brandId, dto);

if (result == null)
    return Ok(new { succeeded = false, errors = new[] { "Unexpected error" } });

if (!result.Succeeded)
    return Ok(result);

if (result.Data == null)
    return Ok(new { succeeded = false, errors = new[] { "Product not created" } });

return CreatedAtAction(nameof(GetById), new { id = result.Data.ProductId }, result.Data);
}

       [HttpPut("{id}")]
[Authorize(Policy = "BrandOwnerOnly")]
public async Task<IActionResult> Update(int id, [FromForm] UpdateProductDto dto)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);

    var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    var result = await _productService.UpdateProductAsync(id, userId, dto);

    if (!result.Succeeded)
        return BadRequest(result.Errors);

    return Ok(result.Data);
}

        // Owner يحذف Product
        [HttpDelete("{id}")]
        [Authorize(Policy = "BrandOwnerOnly")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _productService.DeleteProductAsync(id, userId);
            if (!result.Succeeded) return BadRequest(result.Errors);
            return Ok(new { message = result.Message });
        }

     [HttpPost("generate-description")]
// [Authorize(Policy = "BrandOwnerOnly")]
public async Task<IActionResult> GenerateDescription(
    [FromBody] GenerateDescriptionDto dto)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);

    var result = await _descriptionService.GenerateDescriptionAsync(dto);

    if (!result.Succeeded)
        return BadRequest(result.Errors);

    return Ok(new
    {
        suggestion = result.Data
    });
}
    }
}