using Graduation_Project.BLL.Common;
using Graduation_Project.BLL.DTOs.Contract;
using Graduation_Project.BLL.Services.Interfaces;
using Graduation_Project.DAL.Models.Enums;
using Graduation_Project.DAL.Repositories.Interfaces;

namespace Graduation_Project.BLL.Services.Implementations
{
    public class ContractService : IContractService
    {
        private readonly IUnitOfWork _unitOfWork;

        // النص الثابت للعقد
        private const string ContractText = """
            شروط وأحكام انضمام أصحاب البراندات - Local Brand Platform

            1. الالتزام بمعايير الجودة
               - يجب أن تكون جميع المنتجات أصلية ومطابقة للمواصفات المعلنة.
               - لا يُسمح ببيع منتجات مقلدة أو مخالفة للقانون.

            2. سياسة التسعير
               - يحق لصاحب البراند تحديد أسعار منتجاته بحرية.
               - يجب أن تكون الأسعار واضحة وشاملة لجميع الرسوم.

            3. خدمة العملاء
               - يلتزم صاحب البراند بالرد على استفسارات العملاء خلال 48 ساعة.
               - يجب معالجة الشكاوى بجدية واحترافية.

            4. سياسة الإرجاع والاستبدال
               - يجب توضيح سياسة الإرجاع بشكل واضح.
               - الالتزام بإرجاع المبالغ في حالة المنتجات المعيبة.

            5. حقوق المنصة
               - تحتفظ المنصة بحق إيقاف أي براند يخالف هذه الشروط.
               - يحق للمنصة أخذ نسبة متفق عليها من كل عملية بيع.

            بالموافقة على هذه الشروط، أنت تقر بأنك قرأت وفهمت وتوافق على جميع البنود المذكورة أعلاه.
            """;

        public ContractService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // يجيب نص العقد
        public ServiceResult<ContractDto> GetContract()
        {
            return ServiceResult<ContractDto>.Success(new ContractDto
            {
                ContractText = ContractText
            });
        }

        // الأونر يوافق على العقد
        public async Task<ServiceResult<bool>> AcceptContractAsync(int userId)
        {
            var user = await _unitOfWork.ApplicationUsers.GetByIdAsync(userId);
            if (user == null)
                return ServiceResult<bool>.Failure("User not found");

            // لازم يكون BrandOwner
            if (user.UserType != UserType.BrandOwner)
                return ServiceResult<bool>.Failure("Only brand owners can accept the contract");

            // لو وافق قبل كده
            if (user.HasAcceptedContract)
                return ServiceResult<bool>.Failure("You have already accepted the contract");

            user.HasAcceptedContract = true;
            user.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.ApplicationUsers.Update(user);
            await _unitOfWork.SaveAsync();

            return ServiceResult<bool>.Success(true, "Contract accepted successfully. You can now create your brand!");
        }

        // يشوف الأونر وافق ولا لأ
        public async Task<ServiceResult<ContractStatusDto>> GetContractStatusAsync(int userId)
        {
            var user = await _unitOfWork.ApplicationUsers.GetByIdAsync(userId);
            if (user == null)
                return ServiceResult<ContractStatusDto>.Failure("User not found");

            return ServiceResult<ContractStatusDto>.Success(new ContractStatusDto
            {
                HasAcceptedContract = user.HasAcceptedContract,
                Message = user.HasAcceptedContract
                    ? "Contract accepted. You can create your brand."
                    : "Please accept the contract to start creating your brand."
            });
        }
    }
}