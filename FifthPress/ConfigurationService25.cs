using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using FifthPress.Models;

namespace FifthPress
{
    public class ConfigurationService25
    {
        private readonly FifthPressEntities _db;

        public ConfigurationService25(FifthPressEntities db)
        {
            _db = db;
        }

        /// <summary>
        /// Get a configuration value with a default fallback
        /// </summary>
        public T GetConfigValue<T>(string key, T defaultValue)
        {
            try
            {
                var config = _db.PO_Configuration25
                    .FirstOrDefault(c => c.ConfigKey == key && c.IsActive);

                if (config == null)
                {
                    // Create the config with default value if it doesn't exist
                    SetConfigValue(key, defaultValue, $"Auto-created config for {key}");
                    return defaultValue;
                }

                return ConvertConfigValue<T>(config.ConfigValue, config.DataType, defaultValue);
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Set a configuration value
        /// </summary>
        public bool SetConfigValue<T>(string key, T value, string description = null, string category = "General", string modifiedBy = "System")
        {
            try
            {
                var config = _db.PO_Configuration25
                    .FirstOrDefault(c => c.ConfigKey == key);

                var dataType = GetDataType(typeof(T));
                var stringValue = ConvertToString(value);

                if (config == null)
                {
                    // Create new configuration
                    config = new PO_Configuration25
                    {
                        ConfigKey = key,
                        ConfigValue = stringValue,
                        DataType = dataType,
                        Description = description ?? $"Configuration setting for {key}",
                        Category = category,
                        IsSystemConfig = false,
                        CreatedDate = DateTime.Now,
                        ModifiedBy = modifiedBy,
                        IsActive = true
                    };
                    _db.PO_Configuration25.Add(config);
                }
                else
                {
                    // Update existing configuration
                    config.ConfigValue = stringValue;
                    config.DataType = dataType;
                    config.ModifiedDate = DateTime.Now;
                    config.ModifiedBy = modifiedBy;

                    if (!string.IsNullOrEmpty(description))
                        config.Description = description;

                    if (!string.IsNullOrEmpty(category))
                        config.Category = category;
                }

                _db.SaveChanges();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Get all configurations by category
        /// </summary>
        public List<PO_Configuration25> GetConfigurationsByCategory(string category)
        {
            return _db.PO_Configuration25
                .Where(c => c.Category == category && c.IsActive)
                .OrderBy(c => c.ConfigKey)
                .ToList();
        }

        /// <summary>
        /// Get all configuration categories
        /// </summary>
        public List<string> GetCategories()
        {
            return _db.PO_Configuration25
                .Where(c => c.IsActive)
                .Select(c => c.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        /// <summary>
        /// Initialize default system configurations
        /// </summary>
        public void InitializeDefaultConfigurations()
        {
            var defaultConfigs = new Dictionary<string, object>
            {
                // SLA Settings
                { "StandardApprovalHours", 48 },
                { "OwnerApprovalHours", 72 },
                { "WarningThresholdHours", 6 },
                { "EscalationEnabled", true },

                // Budget Settings
                { "BudgetAlertThresholdPercent", 90 },
                { "AllowBudgetOveragesByDefault", false },
                { "BudgetYearStartMonth", 1 }, // January = 1

                // Email Settings
                { "NotificationsEnabled", true },
                { "AdminEmailAddress", "admin@yourcompany.com" },
                { "EmailFromAddress", "noreply@yourcompany.com" },
                { "EmailSignature", "Purchase Order System" },

                // Approval Settings
                { "DepartmentApproverLimit", 1000 },
                { "DepartmentManagerLimit", 5000 },
                { "RequireOwnerApprovalAmount", 5000 },
                { "AutoApproveExecutiveUnder", 5000 },
                { "AutoApproveAccountingUnder", 2500 },
                { "AutoApprovePurchasingUnder", 2500 },

                // System Settings
                { "SystemMaintenanceMode", false },
                { "MaxLineItemsPerPO", 50 },
                { "DefaultTaxRate", 0.0875m }, // 8.75%
                { "CurrencySymbol", "$" },
                { "DateFormat", "MM/dd/yyyy" },

                // File Upload Settings
                { "MaxFileUploadSizeMB", 10 },
                { "AllowedFileExtensions", ".pdf,.doc,.docx,.xls,.xlsx,.jpg,.jpeg,.png" },

                // Reporting Settings
                { "ReportRetentionDays", 365 },
                { "EnableAdvancedReporting", false }
            };

            foreach (var defaultConfig in defaultConfigs)
            {
                var key = defaultConfig.Key;
                var value = defaultConfig.Value;

                // Only create if it doesn't exist
                if (!_db.PO_Configuration25.Any(c => c.ConfigKey == key))
                {
                    var category = GetCategoryFromKey(key);
                    var description = GetDescriptionFromKey(key);

                    var config = new PO_Configuration25
                    {
                        ConfigKey = key,
                        ConfigValue = ConvertToString(value),
                        DataType = GetDataType(value.GetType()),
                        Description = description,
                        Category = category,
                        IsSystemConfig = true,
                        CreatedDate = DateTime.Now,
                        ModifiedBy = "System",
                        IsActive = true
                    };

                    _db.PO_Configuration25.Add(config);
                }
            }

            _db.SaveChanges();
        }

        /// <summary>
        /// Delete a configuration (only if not system config)
        /// </summary>
        public bool DeleteConfiguration(string key)
        {
            try
            {
                var config = _db.PO_Configuration25
                    .FirstOrDefault(c => c.ConfigKey == key && !c.IsSystemConfig);

                if (config != null)
                {
                    config.IsActive = false;
                    config.ModifiedDate = DateTime.Now;
                    _db.SaveChanges();
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #region Private Helper Methods

        private T ConvertConfigValue<T>(string value, string dataType, T defaultValue)
        {
            try
            {
                if (string.IsNullOrEmpty(value))
                    return defaultValue;

                var targetType = typeof(T);

                // Handle nullable types
                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    targetType = Nullable.GetUnderlyingType(targetType);
                }

                if (targetType == typeof(string))
                    return (T)(object)value;
                else if (targetType == typeof(int))
                    return (T)(object)int.Parse(value);
                else if (targetType == typeof(decimal))
                    return (T)(object)decimal.Parse(value);
                else if (targetType == typeof(bool))
                    return (T)(object)bool.Parse(value);
                else if (targetType == typeof(DateTime))
                    return (T)(object)DateTime.Parse(value);
                else
                    return (T)Convert.ChangeType(value, targetType);
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        private string ConvertToString<T>(T value)
        {
            if (value == null)
                return string.Empty;

            if (value is bool)
                return value.ToString().ToLower();
            else if (value is DateTime)
                return ((DateTime)(object)value).ToString("yyyy-MM-dd HH:mm:ss");
            else
                return value.ToString();
        }

        private string GetDataType(Type type)
        {
            if (type == typeof(string))
                return "String";
            else if (type == typeof(int) || type == typeof(int?))
                return "Int";
            else if (type == typeof(decimal) || type == typeof(decimal?) || type == typeof(double) || type == typeof(float))
                return "Decimal";
            else if (type == typeof(bool) || type == typeof(bool?))
                return "Boolean";
            else if (type == typeof(DateTime) || type == typeof(DateTime?))
                return "DateTime";
            else
                return "String";
        }

        private string GetCategoryFromKey(string key)
        {
            if (key.Contains("Approval") || key.Contains("SLA") || key.Contains("Escalation"))
                return "SLA";
            else if (key.Contains("Budget"))
                return "Budget";
            else if (key.Contains("Email") || key.Contains("Notification"))
                return "Email";
            else if (key.Contains("File") || key.Contains("Upload"))
                return "FileUpload";
            else if (key.Contains("Report"))
                return "Reporting";
            else
                return "General";
        }

        private string GetDescriptionFromKey(string key)
        {
            var descriptions = new Dictionary<string, string>
            {
                { "StandardApprovalHours", "Hours allowed for standard PO approvals (under $1,000)" },
                { "OwnerApprovalHours", "Hours allowed for owner approval (over $5,000)" },
                { "WarningThresholdHours", "Hours before deadline to send warning notifications" },
                { "EscalationEnabled", "Enable automatic escalation for overdue approvals" },
                { "BudgetAlertThresholdPercent", "Budget usage percentage to trigger alerts" },
                { "AllowBudgetOveragesByDefault", "Allow departments to exceed budget by default" },
                { "NotificationsEnabled", "Enable email notifications system-wide" },
                { "DepartmentApproverLimit", "Maximum dollar amount for department approver" },
                { "DepartmentManagerLimit", "Maximum dollar amount for department manager" },
                { "RequireOwnerApprovalAmount", "Dollar amount requiring owner approval" },
                { "MaxFileUploadSizeMB", "Maximum file upload size in megabytes" },
                { "DefaultTaxRate", "Default tax rate for calculations" }
            };

            return descriptions.ContainsKey(key) ? descriptions[key] : $"Configuration setting for {key}";
        }

        #endregion Private Helper Methods
    }
}