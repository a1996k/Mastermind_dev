using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Web;

namespace FifthPress.Models
{
    /// <summary>
    /// Syntax of DataPermission: {DataName},{PermissionWeight},{IsHeirarchal}
    /// </summary>
    public class DataPermission
    {
        public string DataName { get; set; }
        public int PermissionWeight { get; set; }
        public string ChartName { get; set; }

        //If IsHeirarchal, data attached to users with PermissionWeight's less than this user's weight is visible
        //If not, only this user's data is available
        public bool IsHeirarchal { get; set; }
    }
}