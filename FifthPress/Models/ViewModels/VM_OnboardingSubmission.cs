using System.Collections.Generic;

namespace FifthPress.Models.ViewModels
{
    public class VM_OnboardingSubmission
    {
        public string Token { get; set; }
        public string LegalCompanyName { get; set; }
        public VM_OnboardingBillingAddress BillingAddress { get; set; }
        public List<VM_OnboardingShippingAddress> ShippingAddresses { get; set; }
        public VM_OnboardingContact ApContact { get; set; }
        public VM_OnboardingContact PurchasingContact { get; set; }
        public string ThirdPartyShippingAccount { get; set; }
        public bool RequestCreditCheck { get; set; }
        public List<VM_CreditReference> CreditReferences { get; set; }
        public bool TaxExempt { get; set; }
    }

    public class VM_OnboardingBillingAddress
    {
        public string Address { get; set; }
        public string City { get; set; }
        public int? StateId { get; set; }
        public string Zip { get; set; }
    }

    public class VM_OnboardingShippingAddress
    {
        public string Address { get; set; }
        public string Address2 { get; set; }
        public string City { get; set; }
        public int? StateId { get; set; }
        public string Zip { get; set; }
        public string Phone { get; set; }
        public string Attention { get; set; }
        public string CustomerName { get; set; }
        public bool IsDefault { get; set; }
    }

    public class VM_OnboardingContact
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
    }

    public class VM_CreditReference
    {
        public string CompanyName { get; set; }
        public string ContactName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
    }
}
