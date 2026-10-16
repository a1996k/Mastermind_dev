using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI.WebControls;

namespace FifthPress.Models.ControllerModels
{
    public class ServiceAssignment_CM
    {
        public int recordId { get; set; }
        public List<int> selectedUsers { get; set; }
    }
}