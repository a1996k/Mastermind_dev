using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace FifthPress.Models.ViewModels
{
    public class VM_CreateClientLogin
    {
        public string Name { get; set; }

        [Required(ErrorMessage = "At least one email is required")]
        public List<EmailEntryViewModel> Emails { get; set; }

        [Required(ErrorMessage = "At least one client must be selected")]
        public List<int> SelectedClientIds { get; set; }

        // For populating the dropdown in the view
        public List<ClientDropdownItem> AvailableClients { get; set; }

        public VM_CreateClientLogin()
        {
            Emails = new List<EmailEntryViewModel>();
            SelectedClientIds = new List<int>();
            AvailableClients = new List<ClientDropdownItem>();
        }
    }

    public class EmailEntryViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; }

        public bool IsPrimary { get; set; }
    }

    public class ClientDropdownItem
    {
        public int Id { get; set; }
        public string Title { get; set; }
    }
}
