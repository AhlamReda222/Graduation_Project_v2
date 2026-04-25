using Graduation_Project.BLL.DTOs.BrandOwnerRequest;
using Graduation_Project.BLL.Services.Interfaces;
using Graduation_Project.DAL.Models.Entities;
using Graduation_Project.DAL.Models.Enums;
using Graduation_Project.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Graduation_Project.BLL.Services.Implementations
{
    public class BrandOwnerRequestService : IBrandOwnerRequestService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileService _fileService;

        public BrandOwnerRequestService(IUnitOfWork unitOfWork, IFileService fileService)
        {
            _unitOfWork = unitOfWork;
            _fileService = fileService;
        }

        public async Task<BrandOwnerRequestDto> CreateRequestAsync(int userId, CreateBrandOwnerRequestDto dto)
        {
            var user = await _unitOfWork.ApplicationUsers.GetByIdAsync(userId);
            if (user == null)
                throw new KeyNotFoundException("User not found");

            if (await HasPendingRequestAsync(userId))
                throw new InvalidOperationException("You already have a pending request");

            if (user.UserType == UserType.BrandOwner)
                throw new InvalidOperationException("You are already a brand owner");

            // ✅ رفع License (ملف)
            string? licenseUrl = null;
            if (dto.BusinessLicense != null)
            {
                licenseUrl = await _fileService.UploadFileAsync(dto.BusinessLicense, "licenses");
            }

            // ✅ رفع Logo (صورة)
            string? logoUrl = null;
            if (dto.BrandLogo != null)
            {
                logoUrl = await _fileService.UploadFileAsync(dto.BrandLogo, "logos");
            }

            var request = new BrandOwnerRequest
            {
                UserId = userId,
                BusinessName = dto.BusinessName,
                BusinessLicense = licenseUrl,
                BrandName = dto.BrandName,
                BrandDescription = dto.BrandDescription,
                BrandLogoUrl = logoUrl,
                RequestStatus = RequestStatus.Pending,
                RequestDate = DateTime.UtcNow
            };

            await _unitOfWork.BrandOwnerRequests.AddAsync(request);
            await _unitOfWork.SaveAsync();

            return await GetRequestByIdAsync(request.RequestId);
        }

        public async Task<BrandOwnerRequestDto> GetRequestByIdAsync(int requestId)
        {
            var request = await _unitOfWork.BrandOwnerRequests
                .GetQueryable()
                .Include(r => r.User)
                .Include(r => r.Reviewer)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);

            if (request == null)
                throw new KeyNotFoundException("Request not found");

            return MapToDto(request);
        }

        public async Task<IEnumerable<BrandOwnerRequestDto>> GetAllRequestsAsync()
        {
            var requests = await _unitOfWork.BrandOwnerRequests
                .GetQueryable()
                .Include(r => r.User)
                .Include(r => r.Reviewer)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return requests.Select(MapToDto);
        }

        public async Task<IEnumerable<BrandOwnerRequestDto>> GetUserRequestsAsync(int userId)
        {
            var requests = await _unitOfWork.BrandOwnerRequests
                .GetQueryable()
                .Include(r => r.User)
                .Include(r => r.Reviewer)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return requests.Select(MapToDto);
        }

        public async Task<IEnumerable<BrandOwnerRequestDto>> GetPendingRequestsAsync()
        {
            var requests = await _unitOfWork.BrandOwnerRequests
                .GetQueryable()
                .Include(r => r.User)
                .Where(r => r.RequestStatus == RequestStatus.Pending)
                .OrderBy(r => r.RequestDate)
                .ToListAsync();

            return requests.Select(MapToDto);
        }

        public async Task<BrandOwnerRequestDto> ApproveRequestAsync(int requestId, int adminId)
        {
            var request = await _unitOfWork.BrandOwnerRequests
                .GetQueryable()
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);

            if (request == null)
                throw new KeyNotFoundException("Request not found");

            if (request.RequestStatus != RequestStatus.Pending)
                throw new InvalidOperationException("Only pending requests can be approved");

            request.RequestStatus = RequestStatus.Approved;
            request.ReviewedBy = adminId;
            request.ReviewDate = DateTime.UtcNow;

            if (request.User != null)
                request.User.UserType = UserType.BrandOwner;

            // ✅ إنشاء البراند
            var brand = new Brand
            {
                UserId = request.UserId,
                BrandName = request.BrandName,
                Description = request.BrandDescription,
                LogoUrl = request.BrandLogoUrl,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _unitOfWork.Brands.AddAsync(brand);
            _unitOfWork.BrandOwnerRequests.Update(request);
            await _unitOfWork.SaveAsync();

            return await GetRequestByIdAsync(requestId);
        }

        public async Task<BrandOwnerRequestDto> RejectRequestAsync(int requestId, int adminId)
        {
            var request = await _unitOfWork.BrandOwnerRequests
                .GetQueryable()
                .FirstOrDefaultAsync(r => r.RequestId == requestId);

            if (request == null)
                throw new KeyNotFoundException("Request not found");

            if (request.RequestStatus != RequestStatus.Pending)
                throw new InvalidOperationException("Only pending requests can be rejected");

            request.RequestStatus = RequestStatus.Rejected;
            request.ReviewedBy = adminId;
            request.ReviewDate = DateTime.UtcNow;

            _unitOfWork.BrandOwnerRequests.Update(request);
            await _unitOfWork.SaveAsync();

            return await GetRequestByIdAsync(requestId);
        }

        public async Task<bool> DeleteRequestAsync(int requestId)
        {
            var request = await _unitOfWork.BrandOwnerRequests.GetByIdAsync(requestId);
            if (request == null)
                return false;

            _unitOfWork.BrandOwnerRequests.Delete(request);
            await _unitOfWork.SaveAsync();

            return true;
        }

        public async Task<bool> HasPendingRequestAsync(int userId)
        {
            return await _unitOfWork.BrandOwnerRequests
                .AnyAsync(r => r.UserId == userId && r.RequestStatus == RequestStatus.Pending);
        }

        private BrandOwnerRequestDto MapToDto(BrandOwnerRequest request)
        {
            return new BrandOwnerRequestDto
            {
                RequestId = request.RequestId,
                UserId = request.UserId,
                UserName = request.User?.UserName,
                UserEmail = request.User?.Email,
                BusinessName = request.BusinessName,
                BusinessLicense = request.BusinessLicense,
                BrandName = request.BrandName,
                BrandDescription = request.BrandDescription,
                BrandLogoUrl = request.BrandLogoUrl,
                RequestStatus = request.RequestStatus,
                RequestStatusText = GetStatusText(request.RequestStatus),
                RequestDate = request.RequestDate,
                ReviewedBy = request.ReviewedBy,
                ReviewerName = request.Reviewer?.UserName,
                ReviewDate = request.ReviewDate
            };
        }

        private string GetStatusText(RequestStatus status)
        {
            return status switch
            {
                RequestStatus.Pending => "Pending",
                RequestStatus.Approved => "Approved",
                RequestStatus.Rejected => "Rejected",
                _ => "Unknown"
            };
        }
    }
}