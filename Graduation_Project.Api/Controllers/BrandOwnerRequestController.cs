using Graduation_Project.BLL.DTOs.BrandOwnerRequest;
using Graduation_Project.BLL.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Graduation_Project.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // كل ال endpoints محتاجة authentication
    public class BrandOwnerRequestController : ControllerBase
    {
        private readonly IBrandOwnerRequestService _service;

        public BrandOwnerRequestController(IBrandOwnerRequestService service)
        {
            _service = service;
        }
        [HttpPost("request-owner")]
        [Authorize(Policy = "CustomerOnly")] // تستخدم الـ Policy بدل الـ Role
public async Task<IActionResult> CreateRequest([FromBody] CreateBrandOwnerRequestDto dto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _service.CreateRequestAsync(userId, dto);
            return Ok(result);
        }

        // Admin يشوف كل الطلبات
        [HttpGet]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _service.GetAllRequestsAsync();
            return Ok(result);
        }

        // Admin يشوف الطلبات المعلقة
        [HttpGet("pending")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetPending()
        {
            var result = await _service.GetPendingRequestsAsync();
            return Ok(result);
        }

        // Admin يوافق على طلب
        [HttpPut("{requestId}/approve")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Approve(int requestId)
        {
            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _service.ApproveRequestAsync(requestId, adminId);
            return Ok(result);
        }

        // Admin يرفض طلب
        [HttpPut("{requestId}/reject")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Reject(int requestId)
        {
            var adminId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _service.RejectRequestAsync(requestId, adminId);
            return Ok(result);
        }

        // Customer يشوف طلباته
        [HttpGet("my-requests")]
[Authorize(Policy = "CustomerOnly")]
        public async Task<IActionResult> GetMyRequests()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var result = await _service.GetUserRequestsAsync(userId);
            return Ok(result);
        }

        // Admin يحذف طلب
        [HttpDelete("{requestId}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Delete(int requestId)
        {
            var result = await _service.DeleteRequestAsync(requestId);
            return result ? Ok(new { message = "Deleted successfully" }) : NotFound();
        }
    }
}