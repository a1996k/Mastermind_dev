// =============================================
// Tournament Budget Management JavaScript
// Handles dynamic budget line item management
// =============================================

var budgetData = null;
var budgetCategories = null;
var currentCategory = 'Hardware Rental';

// =============================================
// Load Budget Data
// =============================================
function loadBudgetData(clientId) {
    console.log('Loading budget data for client:', clientId);

    // Load categories first
    $.ajax({
        url: ProcessUrl('Administration/GetBudgetCategories'),
        type: 'GET',
        success: function (response) {
            if (response.success) {
                budgetCategories = response.data;
                console.log('Categories loaded:', budgetCategories);

                // Then load budget data
                loadClientBudget(clientId);
            } else {
                console.error('Failed to load categories:', response.message);
                showBudgetError('Failed to load budget categories');
            }
        },
        error: function (xhr, status, error) {
            console.error('Error loading categories:', error);
            showBudgetError('Error loading budget categories');
        }
    });
}

function loadClientBudget(clientId) {
    $.ajax({
        url: ProcessUrl('Administration/GetClientBudget'),
        type: 'GET',
        data: { clientId: clientId },
        success: function (response) {
            if (response.success) {
                budgetData = response.data;
                console.log('Budget data loaded:', budgetData);

                // Populate settings
                $('#tournamentLocation').val(budgetData.TournamentLocation || '');
                $('#taxRate').val(budgetData.TaxRate || 7.00);
                $('#budgetNotes').val(budgetData.Notes || '');
                $('#taxRateDisplay').text(budgetData.TaxRate || 7.00);

                // Load onsite print info
                loadOnsitePrintInfo(clientId);

                // Render categories
                renderAllCategories();

                // Show first category
                showCategory('Hardware Rental');

                // Calculate totals
                updateBudgetSummary();

                // Show content
                $('.budget-loading').hide();
                $('#budgetEditorContent').show();
            } else {
                console.error('Failed to load budget:', response.message);
                showBudgetError('Failed to load budget data');
            }
        },
        error: function (xhr, status, error) {
            console.error('Error loading budget:', error);
            showBudgetError('Error loading budget data');
        }
    });
}

function showBudgetError(message) {
    $('.budget-loading').html('<div class="alert alert-danger">' + message + '</div>');
}

// =============================================
// Render Category Tables
// =============================================
function renderAllCategories() {
    var categoryNames = [
        'Hardware Rental',
        'Offsite Labor',
        'Travel Expenses',
        'Hotel/Accommodation',
        'Vehicle Rental',
        'Freight',
        'Reimbursables - Hardware',
        'Management Fee',
        'Storage Fee',
        'Design Fee',
        'Miscellaneous'
    ];

    var container = $('#categoryContentContainer');

    // Preserve the Onsite Print Info static HTML
    var onsitePrintInfoHtml = $('.budget-category-content[data-category="Onsite Print Info"]').prop('outerHTML');

    container.empty();

    // Re-add Onsite Print Info first
    if (onsitePrintInfoHtml) {
        container.append(onsitePrintInfoHtml);
    }

    categoryNames.forEach(function (categoryName) {
        var categoryHtml = renderCategoryTable(categoryName);
        container.append(categoryHtml);
    });
}

function renderCategoryTable(categoryName) {
    var categoryKey = getCategoryKey(categoryName);
    var lineItems = budgetData[categoryKey] || [];
    var category = getCategoryByCategoryName(categoryName);

    var html = '<div class="budget-category-content" data-category="' + categoryName + '">';
    html += '<h4>' + categoryName + '</h4>';
    html += '<button type="button" class="btn btn-success btn-sm btn-add-line-item" data-category="' + categoryName + '">Add Line Item</button>';

    if (lineItems.length === 0) {
        html += '<p class="text-muted">No line items yet. Click "Add Line Item" to create one.</p>';
    } else {
        html += '<table class="budget-line-item-table">';
        html += '<thead><tr>';
        html += '<th>Description</th>';
        html += '<th width="80">Qty</th>';
        html += '<th width="80">Days</th>';
        html += '<th width="100">Unit Price</th>';
        html += '<th width="120">Estimated</th>';
        html += '<th width="120">Actual</th>';
        // html += '<th width="100">Variance</th>';
        html += '<th width="100">Actions</th>';
        html += '</tr></thead>';
        html += '<tbody>';

        lineItems.forEach(function (item) {
            html += renderLineItemRow(item);
        });

        html += '</tbody>';
        html += '</table>';

        // Category totals
        var categoryEstimated = lineItems.reduce(function (sum, item) { return sum + (item.EstimatedAmount || 0); }, 0);
        var categoryActual = lineItems.reduce(function (sum, item) { return sum + (item.ActualAmount || 0); }, 0);
        // var categoryVariance = categoryActual - categoryEstimated;

        html += '<div class="category-totals" style="text-align: right; padding: 10px; background: #f8f9fa; border-radius: 5px;">';
        html += '<strong>Category Totals: </strong>';
        html += 'Estimated: ' + formatCurrency(categoryEstimated) + ' | ';
        html += 'Actual: ' + formatCurrency(categoryActual);
        // html += ' | Variance: <span class="' + (categoryVariance >= 0 ? 'variance-negative' : 'variance-positive') + '">' + formatCurrency(categoryVariance) + '</span>';
        html += '</div>';
    }

    html += '</div>';
    return html;
}

function renderLineItemRow(item) {
    // var variance = (item.ActualAmount || 0) - item.EstimatedAmount;
    // var varianceClass = variance < 0 ? 'variance-positive' : 'variance-negative';

    var html = '<tr data-line-item-id="' + item.LineItemId + '">';
    html += '<td>' + escapeHtml(item.ItemDescription) + '</td>';
    html += '<td>' + (item.Quantity || '-') + '</td>';
    html += '<td>' + (item.QuantityDays || '-') + '</td>';
    html += '<td>' + (item.UnitPrice ? formatCurrency(item.UnitPrice) : '-') + '</td>';
    html += '<td>' + formatCurrency(item.EstimatedAmount) + '</td>';
    html += '<td>' + (item.ActualAmount ? formatCurrency(item.ActualAmount) : '-') + '</td>';
    // html += '<td class="' + varianceClass + '">' + formatCurrency(variance) + '</td>';
    html += '<td>';
    html += '<button type="button" class="btn btn-sm btn-primary btn-edit-line-item" data-line-item-id="' + item.LineItemId + '">Edit</button> ';
    html += '<button type="button" class="btn btn-sm btn-danger btn-delete-line-item" data-line-item-id="' + item.LineItemId + '">Delete</button>';
    html += '</td>';
    html += '</tr>';

    return html;
}

// =============================================
// Category Navigation
// =============================================
function showCategory(categoryName) {
    currentCategory = categoryName;

    // Update button states
    $('.budget-category-btn').removeClass('active');
    $('.budget-category-btn[data-category="' + categoryName + '"]').addClass('active');

    // Update content visibility
    $('.budget-category-content').removeClass('active');
    $('.budget-category-content[data-category="' + categoryName + '"]').addClass('active');
}

// =============================================
// Onsite Print Info Functions
// =============================================
function loadOnsitePrintInfo(clientId) {
    $.ajax({
        url: ProcessUrl('Administration/GetOnsitePrintInfo'),
        type: 'GET',
        data: { clientId: clientId },
        success: function (response) {
            if (response.success) {
                $('#onsitePrintBaseId').val(response.baseId || '');
                $('#onsitePrintSqftPrice').val(response.sqftPrice || '');
                console.log('Onsite print info loaded:', response);
            } else {
                console.warn('Failed to load onsite print info:', response.message);
            }
        },
        error: function (xhr, status, error) {
            console.error('Error loading onsite print info:', error);
        }
    });
}

function saveOnsitePrintInfo() {
    var clientId = $('#budgetEditorContainer').data('client-id');
    var baseId = $('#onsitePrintBaseId').val();
    var sqftPrice = parseFloat($('#onsitePrintSqftPrice').val()) || null;

    $.ajax({
        url: ProcessUrl('Administration/SaveOnsitePrintInfo'),
        type: 'POST',
        data: {
            clientId: clientId,
            baseId: baseId,
            sqftPrice: sqftPrice
        },
        success: function (response) {
            if (response.success) {
                alert('Onsite print info saved successfully');
            } else {
                alert('Error saving onsite print info: ' + response.message);
            }
        },
        error: function (xhr, status, error) {
            alert('Error saving onsite print info: ' + error);
        }
    });
}

// =============================================
// Save Budget Settings
// =============================================
function saveBudgetSettings() {
    var clientId = $('#budgetEditorContainer').data('client-id');
    var tournamentLocation = $('#tournamentLocation').val();
    var taxRate = parseFloat($('#taxRate').val()) || 7.00;
    var notes = $('#budgetNotes').val();

    $.ajax({
        url: ProcessUrl('Administration/SaveBudgetSettings'),
        type: 'POST',
        data: {
            clientId: clientId,
            tournamentLocation: tournamentLocation,
            taxRate: taxRate,
            notes: notes
        },
        success: function (response) {
            if (response.success) {
                alert('Budget settings saved successfully');
                budgetData.TaxRate = taxRate;
                $('#taxRateDisplay').text(taxRate);
                updateBudgetSummary();
            } else {
                alert('Error saving settings: ' + response.message);
            }
        },
        error: function (xhr, status, error) {
            alert('Error saving settings: ' + error);
        }
    });
}

// =============================================
// Line Item CRUD Operations
// =============================================
function openLineItemModal(categoryName, lineItemId) {
    var category = getCategoryByCategoryName(categoryName);
    if (!category) {
        alert('Category not found');
        return;
    }

    $('#editCategoryId').val(category.CategoryId);
    $('#editCategoryName').val(categoryName);

    if (lineItemId) {
        // Edit existing line item
        var lineItem = findLineItem(lineItemId);
        if (!lineItem) {
            alert('Line item not found');
            return;
        }

        $('#lineItemModalTitle').text('Edit Line Item');
        $('#editLineItemId').val(lineItem.LineItemId);
        $('#editItemDescription').val(lineItem.ItemDescription);
        $('#editQuantity').val(lineItem.Quantity || '');
        $('#editQuantityDays').val(lineItem.QuantityDays || '');
        $('#editUnitPrice').val(lineItem.UnitPrice || '');
        $('#editSqFt').val(lineItem.SqFt || '');
        $('#editWidth').val(lineItem.Width || '');
        $('#editHeight').val(lineItem.Height || '');
        $('#editStartDate').val(lineItem.StartDate ? formatDateForInput(lineItem.StartDate) : '');
        $('#editEndDate').val(lineItem.EndDate ? formatDateForInput(lineItem.EndDate) : '');
        $('#editEstimatedAmount').val(lineItem.EstimatedAmount || '');
        $('#editActualAmount').val(lineItem.ActualAmount || '');
        $('#editIsTaxable').prop('checked', lineItem.IsTaxable || false);
        $('#editNotes').val(lineItem.Notes || '');
    } else {
        // New line item
        $('#lineItemModalTitle').text('Add Line Item - ' + categoryName);
        $('#editLineItemId').val('0');
        clearLineItemForm();
    }

    $('#lineItemModal').modal('show');
}

function clearLineItemForm() {
    $('#editItemDescription').val('');
    $('#editQuantity').val('');
    $('#editQuantityDays').val('');
    $('#editUnitPrice').val('');
    $('#editSqFt').val('');
    $('#editWidth').val('');
    $('#editHeight').val('');
    $('#editStartDate').val('');
    $('#editEndDate').val('');
    $('#editEstimatedAmount').val('');
    $('#editActualAmount').val('');
    $('#editIsTaxable').prop('checked', true);
    $('#editNotes').val('');
}

function saveLineItem() {
    var clientId = $('#budgetEditorContainer').data('client-id');
    var lineItemId = parseInt($('#editLineItemId').val()) || 0;
    var categoryId = parseInt($('#editCategoryId').val());
    var categoryName = $('#editCategoryName').val();

    // Validate required fields
    if (!$('#editItemDescription').val()) {
        alert('Item description is required');
        return;
    }

    if (!$('#editEstimatedAmount').val()) {
        alert('Estimated amount is required');
        return;
    }

    // Validate numeric fields for negative values
    var numericFields = [
        { name: 'Quantity', selector: '#editQuantity', label: 'Quantity' },
        { name: 'QuantityDays', selector: '#editQuantityDays', label: 'Days' },
        { name: 'UnitPrice', selector: '#editUnitPrice', label: 'Unit Price' },
        { name: 'SqFt', selector: '#editSqFt', label: 'Sq Ft' },
        { name: 'Width', selector: '#editWidth', label: 'Width' },
        { name: 'Height', selector: '#editHeight', label: 'Height' },
        { name: 'EstimatedAmount', selector: '#editEstimatedAmount', label: 'Estimated Amount' },
        { name: 'ActualAmount', selector: '#editActualAmount', label: 'Actual Amount' }
    ];

    for (var i = 0; i < numericFields.length; i++) {
        var field = numericFields[i];
        var value = $(field.selector).val();
        if (value !== '' && value !== null) {
            var numValue = parseFloat(value);
            if (isNaN(numValue)) {
                alert(field.label + ' must be a valid number');
                return;
            }
            if (numValue < 0) {
                alert(field.label + ' cannot be negative');
                return;
            }
        }
    }

    // Validate date fields
    var startDate = $('#editStartDate').val();
    var endDate = $('#editEndDate').val();

    if (startDate && endDate) {
        var startDateObj = new Date(startDate);
        var endDateObj = new Date(endDate);

        if (endDateObj < startDateObj) {
            alert('End date cannot be before start date');
            return;
        }
    }

    var lineItem = {
        LineItemId: lineItemId,
        ClientId: clientId,
        CategoryId: categoryId,
        CategoryName: categoryName,
        ItemDescription: $('#editItemDescription').val(),
        Quantity: parseFloat($('#editQuantity').val()) || null,
        QuantityDays: parseInt($('#editQuantityDays').val()) || null,
        UnitPrice: parseFloat($('#editUnitPrice').val()) || null,
        SqFt: parseFloat($('#editSqFt').val()) || null,
        Width: parseFloat($('#editWidth').val()) || null,
        Height: parseFloat($('#editHeight').val()) || null,
        StartDate: startDate ? formatDateForServer(startDate) : null,
        EndDate: endDate ? formatDateForServer(endDate) : null,
        EstimatedAmount: parseFloat($('#editEstimatedAmount').val()),
        ActualAmount: parseFloat($('#editActualAmount').val()) || null,
        IsTaxable: $('#editIsTaxable').is(':checked'),
        Notes: $('#editNotes').val() || null
    };

    $.ajax({
        url: ProcessUrl('Administration/SaveBudgetLineItem'),
        type: 'POST',
        data: { item: lineItem },
        success: function (response) {
            if (response.success) {
                $('#lineItemModal').modal('hide');

                // Update the lineItemId if this was a new item
                if (lineItemId === 0) {
                    lineItem.LineItemId = response.lineItemId;
                }

                // Update budgetData
                updateLineItemInBudgetData(lineItem);

                // Re-render the category
                renderAllCategories();
                showCategory(categoryName);

                // Update summary
                updateBudgetSummary();
            } else {
                alert('Error saving line item: ' + response.message);
            }
        },
        error: function (xhr, status, error) {
            alert('Error saving line item: ' + error);
        }
    });
}

function deleteLineItem(lineItemId) {
    if (!confirm('Are you sure you want to delete this line item?')) {
        return;
    }

    $.ajax({
        url: ProcessUrl('Administration/DeleteBudgetLineItem'),
        type: 'POST',
        data: { lineItemId: lineItemId },
        success: function (response) {
            if (response.success) {
                // Remove from budgetData
                removeLineItemFromBudgetData(lineItemId);

                // Re-render
                renderAllCategories();
                showCategory(currentCategory);

                // Update summary
                updateBudgetSummary();
            } else {
                alert('Error deleting line item: ' + response.message);
            }
        },
        error: function (xhr, status, error) {
            alert('Error deleting line item: ' + error);
        }
    });
}

// =============================================
// Budget Data Manipulation
// =============================================
function updateLineItemInBudgetData(lineItem) {
    var categoryKey = getCategoryKey(lineItem.CategoryName);
    if (!budgetData[categoryKey]) {
        budgetData[categoryKey] = [];
    }

    var existingIndex = budgetData[categoryKey].findIndex(function (item) {
        return item.LineItemId === lineItem.LineItemId;
    });

    if (existingIndex >= 0) {
        // Update existing
        budgetData[categoryKey][existingIndex] = lineItem;
    } else {
        // Add new
        budgetData[categoryKey].push(lineItem);
    }
}

function removeLineItemFromBudgetData(lineItemId) {
    var categoryKeys = [
        'HardwareRental', 'OffsiteLabor', 'TravelExpenses', 'Accommodation',
        'VehicleRental', 'Freight', 'ReimbursablesHardware', 'ManagementFee',
        'StorageFee', 'DesignFee', 'Miscellaneous'
    ];

    categoryKeys.forEach(function (key) {
        if (budgetData[key]) {
            budgetData[key] = budgetData[key].filter(function (item) {
                return item.LineItemId !== lineItemId;
            });
        }
    });
}

function findLineItem(lineItemId) {
    var categoryKeys = [
        'HardwareRental', 'OffsiteLabor', 'TravelExpenses', 'Accommodation',
        'VehicleRental', 'Freight', 'ReimbursablesHardware', 'ManagementFee',
        'StorageFee', 'DesignFee', 'Miscellaneous'
    ];

    for (var i = 0; i < categoryKeys.length; i++) {
        var key = categoryKeys[i];
        if (budgetData[key]) {
            var found = budgetData[key].find(function (item) {
                return item.LineItemId === lineItemId;
            });
            if (found) return found;
        }
    }

    return null;
}

// =============================================
// Budget Summary Calculations
// =============================================
function updateBudgetSummary() {
    var totalEstimated = 0;
    var totalActual = 0;

    var categoryKeys = [
        'HardwareRental', 'OffsiteLabor', 'TravelExpenses', 'Accommodation',
        'VehicleRental', 'Freight', 'ReimbursablesHardware', 'ManagementFee',
        'StorageFee', 'DesignFee', 'Miscellaneous'
    ];

    categoryKeys.forEach(function (key) {
        if (budgetData[key]) {
            budgetData[key].forEach(function (item) {
                totalEstimated += item.EstimatedAmount || 0;
                totalActual += item.ActualAmount || 0;
            });
        }
    });

    // var totalVariance = totalActual - totalEstimated;
    var taxRate = budgetData.TaxRate || 7.00;
    var estimatedTax = totalEstimated * (taxRate / 100);
    var actualTax = totalActual * (taxRate / 100);
    var grandTotalEstimated = totalEstimated + estimatedTax;
    var grandTotalActual = totalActual + actualTax;

    $('#totalEstimated').text(formatCurrency(totalEstimated));
    $('#totalActual').text(formatCurrency(totalActual));
    // $('#totalVariance').text(formatCurrency(totalVariance))
    //     .removeClass('variance-positive variance-negative')
    //     .addClass(totalVariance < 0 ? 'variance-positive' : 'variance-negative');
    $('#estimatedTax').text(formatCurrency(estimatedTax));
    $('#actualTax').text(formatCurrency(actualTax));
    $('#grandTotalEstimated').text(formatCurrency(grandTotalEstimated));
    $('#grandTotalActual').text(formatCurrency(grandTotalActual));
}

// =============================================
// Helper Functions
// =============================================
function getCategoryKey(categoryName) {
    var map = {
        'Hardware Rental': 'HardwareRental',
        'Offsite Labor': 'OffsiteLabor',
        'Travel Expenses': 'TravelExpenses',
        'Hotel/Accommodation': 'Accommodation',
        'Vehicle Rental': 'VehicleRental',
        'Freight': 'Freight',
        'Reimbursables - Hardware': 'ReimbursablesHardware',
        'Management Fee': 'ManagementFee',
        'Storage Fee': 'StorageFee',
        'Design Fee': 'DesignFee',
        'Miscellaneous': 'Miscellaneous'
    };
    return map[categoryName] || categoryName;
}

function getCategoryByCategoryName(categoryName) {
    if (!budgetCategories) return null;
    return budgetCategories.find(function (cat) {
        return cat.CategoryName === categoryName;
    });
}

function formatCurrency(value) {
    if (value === null || value === undefined) return '$0.00';
    return '$' + parseFloat(value).toFixed(2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
}

function formatDateForInput(dateString) {
    if (!dateString) return '';
    // Parse the date string as UTC to avoid timezone shifts
    // If the string is "2025-11-04", we want to display "2025-11-04" not shift it
    var parts = dateString.split('T')[0].split('-'); // Get date part only, ignore time
    if (parts.length === 3) {
        return parts[0] + '-' + parts[1] + '-' + parts[2]; // Already in YYYY-MM-DD format
    }
    // Fallback for other date formats
    var date = new Date(dateString);
    var year = date.getFullYear();
    var month = ('0' + (date.getMonth() + 1)).slice(-2);
    var day = ('0' + date.getDate()).slice(-2);
    return year + '-' + month + '-' + day;
}

function formatDateForServer(dateString) {
    if (!dateString) return null;
    // Ensure date is sent in ISO format with no timezone conversion
    // Input is in format "YYYY-MM-DD" from date input
    // We want to send it as "YYYY-MM-DDT00:00:00" to indicate midnight local time
    return dateString + 'T00:00:00';
}

function escapeHtml(text) {
    if (!text) return '';
    var map = {
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#039;'
    };
    return text.replace(/[&<>"']/g, function (m) { return map[m]; });
}

// =============================================
// Event Handlers
// =============================================
$(document).ready(function () {
    // Category navigation
    $(document).on('click', '.budget-category-btn', function () {
        var categoryName = $(this).data('category');
        showCategory(categoryName);
    });

    // Save budget settings
    $(document).on('click', '#saveBudgetSettings', function () {
        saveBudgetSettings();
    });

    // Save onsite print info
    $(document).on('click', '#saveOnsitePrintInfo', function () {
        saveOnsitePrintInfo();
    });

    // Add line item
    $(document).on('click', '.btn-add-line-item', function () {
        var categoryName = $(this).data('category');
        openLineItemModal(categoryName, null);
    });

    // Edit line item
    $(document).on('click', '.btn-edit-line-item', function () {
        var lineItemId = parseInt($(this).data('line-item-id'));
        openLineItemModal(currentCategory, lineItemId);
    });

    // Delete line item
    $(document).on('click', '.btn-delete-line-item', function () {
        var lineItemId = parseInt($(this).data('line-item-id'));
        deleteLineItem(lineItemId);
    });

    // Save line item
    $(document).on('click', '#saveLineItemBtn', function () {
        saveLineItem();
    });
});
