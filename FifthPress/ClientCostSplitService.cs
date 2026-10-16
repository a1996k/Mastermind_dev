using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using FifthPress.Models.ViewModels;

namespace FifthPress
{
    public class ClientCostSplitService
    {
        private readonly FifthPressEntities _db;

        public ClientCostSplitService(FifthPressEntities db)
        {
            _db = db;
        }

        #region Validation

        /// <summary>
        /// Validate that client cost splits sum to exactly 100%
        /// </summary>
        public bool ValidateSplits(List<VM_ClientCostSplit> splits)
        {
            if (splits == null || !splits.Any())
                return false;

            var total = splits.Sum(s => s.SplitPercentage);
            return Math.Abs(total - 100) < 0.01m; // Allow small rounding error
        }

        /// <summary>
        /// Validate splits and return error message if invalid
        /// </summary>
        public (bool IsValid, string ErrorMessage) ValidateSplitsWithMessage(List<VM_ClientCostSplit> splits)
        {
            if (splits == null || !splits.Any())
                return (false, "At least one client allocation is required.");

            // Check for duplicate clients
            var duplicates = splits.GroupBy(s => s.ClientId).Where(g => g.Count() > 1).ToList();
            if (duplicates.Any())
                return (false, "Each client can only appear once in the allocation.");

            // Check percentages are valid
            if (splits.Any(s => s.SplitPercentage <= 0 || s.SplitPercentage > 100))
                return (false, "Each allocation percentage must be between 0.01% and 100%.");

            var total = splits.Sum(s => s.SplitPercentage);
            if (Math.Abs(total - 100) >= 0.01m)
                return (false, $"Allocation percentages must sum to 100%. Current total: {total:F2}%");

            return (true, null);
        }

        #endregion Validation

        #region Job Splits

        /// <summary>
        /// Get client cost splits for a job
        /// </summary>
        public List<VM_ClientCostSplit> GetJobSplits(int jobId)
        {
            return _db.Job_ClientCostSplits
                .Where(s => s.JobId == jobId)
                .Select(s => new VM_ClientCostSplit
                {
                    SplitId = s.SplitId,
                    ClientId = s.ClientId,
                    ClientName = s.Client.Title,
                    SplitPercentage = s.SplitPercentage,
                    IsBaseClient = s.IsBaseClient
                })
                .OrderByDescending(s => s.IsBaseClient)
                .ThenBy(s => s.ClientName)
                .ToList();
        }

        /// <summary>
        /// Get client cost splits for a job with calculated amounts
        /// </summary>
        public List<VM_ClientCostSplit> GetJobSplitsWithAmounts(int jobId, decimal totalAmount)
        {
            var splits = GetJobSplits(jobId);
            foreach (var split in splits)
            {
                split.CalculatedAmount = totalAmount * (split.SplitPercentage / 100);
            }
            return splits;
        }

        /// <summary>
        /// Save client cost splits for a job
        /// </summary>
        public bool SaveJobSplits(int jobId, int baseClientId, List<VM_ClientCostSplit> splits, string userName = null)
        {
            // Ensure base client is in splits
            splits = EnsureBaseClientInSplits(baseClientId, splits);

            var (isValid, errorMessage) = ValidateSplitsWithMessage(splits);
            if (!isValid)
                return false;

            try
            {
                // Remove existing splits for this job
                var existingSplits = _db.Job_ClientCostSplits
                    .Where(s => s.JobId == jobId)
                    .ToList();
                _db.Job_ClientCostSplits.RemoveRange(existingSplits);

                // Add new splits
                foreach (var split in splits)
                {
                    var newSplit = new Job_ClientCostSplits
                    {
                        JobId = jobId,
                        ClientId = split.ClientId,
                        SplitPercentage = split.SplitPercentage,
                        IsBaseClient = split.ClientId == baseClientId,
                        CreatedDate = DateTime.Now,
                        CreatedBy = userName
                    };
                    _db.Job_ClientCostSplits.Add(newSplit);
                }

                _db.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving job client splits: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Initialize default 100% split for base client when creating a new job
        /// </summary>
        public void InitializeJobSplits(int jobId, int baseClientId, string userName = null)
        {
            var existingSplits = _db.Job_ClientCostSplits.Any(s => s.JobId == jobId);
            if (existingSplits)
                return;

            var split = new Job_ClientCostSplits
            {
                JobId = jobId,
                ClientId = baseClientId,
                SplitPercentage = 100.00m,
                IsBaseClient = true,
                CreatedDate = DateTime.Now,
                CreatedBy = userName
            };
            _db.Job_ClientCostSplits.Add(split);
            _db.SaveChanges();
        }

        /// <summary>
        /// Copy splits from one job to another (for Copy Job feature)
        /// </summary>
        public bool CopyJobSplits(int sourceJobId, int targetJobId, int newBaseClientId, string userName = null)
        {
            var sourceSplits = GetJobSplits(sourceJobId);
            if (!sourceSplits.Any())
            {
                InitializeJobSplits(targetJobId, newBaseClientId, userName);
                return true;
            }

            // If source only had base client at 100%, just initialize with new base client
            if (sourceSplits.Count == 1 && sourceSplits[0].IsBaseClient && sourceSplits[0].SplitPercentage == 100)
            {
                InitializeJobSplits(targetJobId, newBaseClientId, userName);
                return true;
            }

            // Copy splits, replacing old base client with new base client
            foreach (var split in sourceSplits)
            {
                var newSplit = new Job_ClientCostSplits
                {
                    JobId = targetJobId,
                    ClientId = split.IsBaseClient ? newBaseClientId : split.ClientId,
                    SplitPercentage = split.SplitPercentage,
                    IsBaseClient = split.IsBaseClient,
                    CreatedDate = DateTime.Now,
                    CreatedBy = userName
                };
                _db.Job_ClientCostSplits.Add(newSplit);
            }

            _db.SaveChanges();
            return true;
        }

        #endregion Job Splits

        #region Quote Splits

        /// <summary>
        /// Get client cost splits for a quote
        /// </summary>
        public List<VM_ClientCostSplit> GetQuoteSplits(int quoteId)
        {
            return _db.Quote_ClientCostSplits
                .Where(s => s.QuoteId == quoteId)
                .Select(s => new VM_ClientCostSplit
                {
                    SplitId = s.SplitId,
                    ClientId = s.ClientId,
                    ClientName = s.Client.Title,
                    SplitPercentage = s.SplitPercentage,
                    IsBaseClient = s.IsBaseClient
                })
                .OrderByDescending(s => s.IsBaseClient)
                .ThenBy(s => s.ClientName)
                .ToList();
        }

        /// <summary>
        /// Get quote cost splits with calculated amounts
        /// </summary>
        public List<VM_ClientCostSplit> GetQuoteSplitsWithAmounts(int quoteId, decimal totalAmount)
        {
            var splits = GetQuoteSplits(quoteId);
            foreach (var split in splits)
            {
                split.CalculatedAmount = totalAmount * (split.SplitPercentage / 100);
            }
            return splits;
        }

        /// <summary>
        /// Save client cost splits for a quote
        /// </summary>
        public bool SaveQuoteSplits(int quoteId, int baseClientId, List<VM_ClientCostSplit> splits, string userName = null)
        {
            // Ensure base client is in splits
            splits = EnsureBaseClientInSplits(baseClientId, splits);

            var (isValid, errorMessage) = ValidateSplitsWithMessage(splits);
            if (!isValid)
                return false;

            try
            {
                // Remove existing splits for this quote
                var existingSplits = _db.Quote_ClientCostSplits
                    .Where(s => s.QuoteId == quoteId)
                    .ToList();
                _db.Quote_ClientCostSplits.RemoveRange(existingSplits);

                // Add new splits
                foreach (var split in splits)
                {
                    var newSplit = new Quote_ClientCostSplits
                    {
                        QuoteId = quoteId,
                        ClientId = split.ClientId,
                        SplitPercentage = split.SplitPercentage,
                        IsBaseClient = split.ClientId == baseClientId,
                        CreatedDate = DateTime.Now,
                        CreatedBy = userName
                    };
                    _db.Quote_ClientCostSplits.Add(newSplit);
                }

                _db.SaveChanges();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving quote client splits: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Initialize default 100% split for base client when creating a new quote
        /// </summary>
        public void InitializeQuoteSplits(int quoteId, int baseClientId, string userName = null)
        {
            var existingSplits = _db.Quote_ClientCostSplits.Any(s => s.QuoteId == quoteId);
            if (existingSplits)
                return;

            var split = new Quote_ClientCostSplits
            {
                QuoteId = quoteId,
                ClientId = baseClientId,
                SplitPercentage = 100.00m,
                IsBaseClient = true,
                CreatedDate = DateTime.Now,
                CreatedBy = userName
            };
            _db.Quote_ClientCostSplits.Add(split);
            _db.SaveChanges();
        }

        /// <summary>
        /// Copy splits from one quote to another
        /// </summary>
        public bool CopyQuoteSplits(int sourceQuoteId, int targetQuoteId, int newBaseClientId, string userName = null)
        {
            var sourceSplits = GetQuoteSplits(sourceQuoteId);
            if (!sourceSplits.Any())
            {
                InitializeQuoteSplits(targetQuoteId, newBaseClientId, userName);
                return true;
            }

            if (sourceSplits.Count == 1 && sourceSplits[0].IsBaseClient && sourceSplits[0].SplitPercentage == 100)
            {
                InitializeQuoteSplits(targetQuoteId, newBaseClientId, userName);
                return true;
            }

            foreach (var split in sourceSplits)
            {
                var newSplit = new Quote_ClientCostSplits
                {
                    QuoteId = targetQuoteId,
                    ClientId = split.IsBaseClient ? newBaseClientId : split.ClientId,
                    SplitPercentage = split.SplitPercentage,
                    IsBaseClient = split.IsBaseClient,
                    CreatedDate = DateTime.Now,
                    CreatedBy = userName
                };
                _db.Quote_ClientCostSplits.Add(newSplit);
            }

            _db.SaveChanges();
            return true;
        }

        /// <summary>
        /// Copy splits from quote to job when converting quote to job
        /// </summary>
        public bool CopyQuoteSplitsToJob(int quoteId, int jobId, int baseClientId, string userName = null)
        {
            var quoteSplits = GetQuoteSplits(quoteId);
            if (!quoteSplits.Any())
            {
                InitializeJobSplits(jobId, baseClientId, userName);
                return true;
            }

            foreach (var split in quoteSplits)
            {
                var newSplit = new Job_ClientCostSplits
                {
                    JobId = jobId,
                    ClientId = split.ClientId,
                    SplitPercentage = split.SplitPercentage,
                    IsBaseClient = split.IsBaseClient,
                    CreatedDate = DateTime.Now,
                    CreatedBy = userName
                };
                _db.Job_ClientCostSplits.Add(newSplit);
            }

            _db.SaveChanges();
            return true;
        }

        #endregion Quote Splits

        #region Cost Calculations

        /// <summary>
        /// Calculate the allocated amount for each client based on total cost
        /// </summary>
        public Dictionary<int, decimal> CalculateSplitAmounts(List<VM_ClientCostSplit> splits, decimal totalCost)
        {
            var result = new Dictionary<int, decimal>();
            foreach (var split in splits)
            {
                result[split.ClientId] = totalCost * (split.SplitPercentage / 100);
            }
            return result;
        }

        /// <summary>
        /// Get a specific client's allocated cost for a job
        /// </summary>
        public decimal GetClientAllocatedJobCost(int jobId, int clientId, decimal totalCost)
        {
            var split = _db.Job_ClientCostSplits
                .FirstOrDefault(s => s.JobId == jobId && s.ClientId == clientId);

            if (split == null)
                return 0;

            return totalCost * (split.SplitPercentage / 100);
        }

        /// <summary>
        /// Get a specific client's allocated cost for a quote
        /// </summary>
        public decimal GetClientAllocatedQuoteCost(int quoteId, int clientId, decimal totalCost)
        {
            var split = _db.Quote_ClientCostSplits
                .FirstOrDefault(s => s.QuoteId == quoteId && s.ClientId == clientId);

            if (split == null)
                return 0;

            return totalCost * (split.SplitPercentage / 100);
        }

        /// <summary>
        /// Get all job IDs where a client has a cost allocation
        /// </summary>
        public List<int> GetJobIdsForClient(int clientId)
        {
            return _db.Job_ClientCostSplits
                .Where(s => s.ClientId == clientId)
                .Select(s => s.JobId)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Get all quote IDs where a client has a cost allocation
        /// </summary>
        public List<int> GetQuoteIdsForClient(int clientId)
        {
            return _db.Quote_ClientCostSplits
                .Where(s => s.ClientId == clientId)
                .Select(s => s.QuoteId)
                .Distinct()
                .ToList();
        }

        #endregion Cost Calculations

        #region Utility Methods

        /// <summary>
        /// Ensure the base client is always in the splits list
        /// </summary>
        public List<VM_ClientCostSplit> EnsureBaseClientInSplits(int baseClientId, List<VM_ClientCostSplit> splits)
        {
            if (splits == null)
                splits = new List<VM_ClientCostSplit>();

            var baseClientSplit = splits.FirstOrDefault(s => s.ClientId == baseClientId);
            if (baseClientSplit == null)
            {
                // Base client not found - this shouldn't happen in normal flow
                // Add base client with remaining percentage
                var remainingPercent = 100 - splits.Sum(s => s.SplitPercentage);
                if (remainingPercent <= 0)
                    remainingPercent = 100; // Default to 100% if somehow we have invalid data

                var baseClient = _db.Clients.Find(baseClientId);
                splits.Insert(0, new VM_ClientCostSplit
                {
                    ClientId = baseClientId,
                    ClientName = baseClient?.Title ?? "Unknown",
                    SplitPercentage = remainingPercent,
                    IsBaseClient = true
                });
            }
            else
            {
                baseClientSplit.IsBaseClient = true;
            }

            return splits;
        }

        /// <summary>
        /// Clear all splits from a job (reset to base client only)
        /// </summary>
        public bool ClearJobSplits(int jobId, int baseClientId, string userName = null)
        {
            try
            {
                var existingSplits = _db.Job_ClientCostSplits
                    .Where(s => s.JobId == jobId)
                    .ToList();
                _db.Job_ClientCostSplits.RemoveRange(existingSplits);

                // Re-initialize with just base client
                InitializeJobSplits(jobId, baseClientId, userName);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing job splits: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clear all splits from a quote (reset to base client only)
        /// </summary>
        public bool ClearQuoteSplits(int quoteId, int baseClientId, string userName = null)
        {
            try
            {
                var existingSplits = _db.Quote_ClientCostSplits
                    .Where(s => s.QuoteId == quoteId)
                    .ToList();
                _db.Quote_ClientCostSplits.RemoveRange(existingSplits);

                InitializeQuoteSplits(quoteId, baseClientId, userName);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing quote splits: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if a job has multiple client allocations
        /// </summary>
        public bool JobHasMultipleClients(int jobId)
        {
            return _db.Job_ClientCostSplits.Count(s => s.JobId == jobId) > 1;
        }

        /// <summary>
        /// Check if a quote has multiple client allocations
        /// </summary>
        public bool QuoteHasMultipleClients(int quoteId)
        {
            return _db.Quote_ClientCostSplits.Count(s => s.QuoteId == quoteId) > 1;
        }

        /// <summary>
        /// Update base client when job's client changes
        /// </summary>
        public void UpdateJobBaseClient(int jobId, int oldBaseClientId, int newBaseClientId, string userName = null)
        {
            var splits = _db.Job_ClientCostSplits.Where(s => s.JobId == jobId).ToList();

            if (!splits.Any())
            {
                InitializeJobSplits(jobId, newBaseClientId, userName);
                return;
            }

            var oldBaseSplit = splits.FirstOrDefault(s => s.ClientId == oldBaseClientId && s.IsBaseClient);
            if (oldBaseSplit != null)
            {
                // Check if new client already has a split
                var existingNewClientSplit = splits.FirstOrDefault(s => s.ClientId == newBaseClientId);
                if (existingNewClientSplit != null)
                {
                    // Merge: add old base percentage to new client and remove old
                    existingNewClientSplit.SplitPercentage += oldBaseSplit.SplitPercentage;
                    existingNewClientSplit.IsBaseClient = true;
                    existingNewClientSplit.ModifiedDate = DateTime.Now;
                    existingNewClientSplit.ModifiedBy = userName;
                    _db.Job_ClientCostSplits.Remove(oldBaseSplit);
                }
                else
                {
                    // Change the base client split to new client
                    oldBaseSplit.ClientId = newBaseClientId;
                    oldBaseSplit.ModifiedDate = DateTime.Now;
                    oldBaseSplit.ModifiedBy = userName;
                }
            }
            else
            {
                // No base split found, initialize fresh
                ClearJobSplits(jobId, newBaseClientId, userName);
                return;
            }

            _db.SaveChanges();
        }

        /// <summary>
        /// Update base client when quote's client changes
        /// </summary>
        public void UpdateQuoteBaseClient(int quoteId, int oldBaseClientId, int newBaseClientId, string userName = null)
        {
            var splits = _db.Quote_ClientCostSplits.Where(s => s.QuoteId == quoteId).ToList();

            if (!splits.Any())
            {
                InitializeQuoteSplits(quoteId, newBaseClientId, userName);
                return;
            }

            var oldBaseSplit = splits.FirstOrDefault(s => s.ClientId == oldBaseClientId && s.IsBaseClient);
            if (oldBaseSplit != null)
            {
                var existingNewClientSplit = splits.FirstOrDefault(s => s.ClientId == newBaseClientId);
                if (existingNewClientSplit != null)
                {
                    existingNewClientSplit.SplitPercentage += oldBaseSplit.SplitPercentage;
                    existingNewClientSplit.IsBaseClient = true;
                    existingNewClientSplit.ModifiedDate = DateTime.Now;
                    existingNewClientSplit.ModifiedBy = userName;
                    _db.Quote_ClientCostSplits.Remove(oldBaseSplit);
                }
                else
                {
                    oldBaseSplit.ClientId = newBaseClientId;
                    oldBaseSplit.ModifiedDate = DateTime.Now;
                    oldBaseSplit.ModifiedBy = userName;
                }
            }
            else
            {
                ClearQuoteSplits(quoteId, newBaseClientId, userName);
                return;
            }

            _db.SaveChanges();
        }

        #endregion Utility Methods
    }
}
