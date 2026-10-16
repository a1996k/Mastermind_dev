using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Web;

namespace FifthPress.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string EntityType { get; set; }
        public string PrimaryKeyValue { get; set; }
        public string PropertyName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public DateTime ChangeDateTime { get; set; }
        public string UserId { get; set; }
    }

}