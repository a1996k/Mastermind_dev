/**
 * Reprint System JavaScript
 * Handles the 4-step reprint wizard modal
 * Author: System Generated
 * Date: 2025-01-03
 */

// =============================================
// GLOBAL STATE
// =============================================

var ReprintWizard = {
    currentStep: 1,
    totalSteps: 4,
    formData: {
        originalJobId: null,
        originalJobNumber: null,
        reprintType: null,
        selectedItems: [],
        reprintReasonId: null,
        reprintReasonText: null,
        reprintCategory: null,
        costAllocation: null,
        estimatedCost: 0,
        description: null,
        internalNotes: null,
        notifyCustomer: false
    },
    jobItems: [],
    reasons: []
};

// =============================================
// INITIALIZATION
// =============================================

$(document).ready(function () {
    initializeEventHandlers();
});

function initializeEventHandlers() {
    // Modal navigation
    $('#btnNext').on('click', handleNextStep);
    $('#btnBack').on('click', handlePreviousStep);
    $('#btnCreate').on('click', handleSubmitReprint);

    // Step 1: Reprint type selection
    $('input[name="ReprintType"]').on('change', handleReprintTypeChange);

    // Step 2: Item selection
    $(document).on('change', '.item-selection-checkbox', handleItemSelectionChange);

    // Step 3: Reason and cost allocation
    $('#reprintReason').on('change', handleReasonChange);
    $('input[name="CostAllocation"]').on('change', updateCostAllocationSummary);

    // Step 4: Description character count
    $('#description').on('input', updateDescriptionCount);

    // Modal reset on close
    $('#reprintModal').on('hidden.bs.modal', resetModal);
}

// =============================================
// MODAL OPEN
// =============================================

/**
 * Opens the reprint modal and loads job data
 * Call this function from job detail page: openReprintModal(jobId)
 */
function openReprintModal(jobId) {
    // Reset wizard
    resetModal();

    // Set job ID
    ReprintWizard.formData.originalJobId = jobId;
    $('#originalJobId').val(jobId);

    // Load job info
    $.ajax({
        url: '/Reprint/GetJobInfo',
        type: 'GET',
        data: { jobId: jobId },
        success: function (response) {
            if (response.success) {
                ReprintWizard.formData.originalJobNumber = response.jobNumber;
                $('#modalJobNumber').text(response.jobNumber);
                $('#reprintModal').modal('show');

                // Pre-load reasons for step 3
                loadReasons();
            } else {
                showError('Failed to load job information: ' + response.message);
            }
        },
        error: function () {
            showError('Error loading job information');
        }
    });
}

// =============================================
// STEP NAVIGATION
// =============================================

function handleNextStep() {
    // Validate current step before proceeding
    if (!validateCurrentStep()) {
        return;
    }

    // Special handling for step transitions
    if (ReprintWizard.currentStep === 1) {
        // Moving from step 1 to step 2 or 3
        if (ReprintWizard.formData.reprintType === 'Partial') {
            // Load items for partial reprint
            loadJobItems();
            goToStep(2);
        } else {
            // Skip step 2 for Full/Additional/Sample
            calculateEstimatedCost();
            goToStep(3);
        }
    } else if (ReprintWizard.currentStep === 2) {
        // Moving from step 2 to step 3
        calculateEstimatedCost();
        goToStep(3);
    } else if (ReprintWizard.currentStep === 3) {
        // Moving from step 3 to step 4
        updateSummary();
        goToStep(4);
    }
}

function handlePreviousStep() {
    if (ReprintWizard.currentStep === 2) {
        // Back to step 1
        goToStep(1);
    } else if (ReprintWizard.currentStep === 3) {
        // Back to step 2 or 1
        if (ReprintWizard.formData.reprintType === 'Partial') {
            goToStep(2);
        } else {
            goToStep(1);
        }
    } else if (ReprintWizard.currentStep === 4) {
        // Back to step 3
        goToStep(3);
    }
}

function goToStep(stepNumber) {
    // Hide all steps
    $('.reprint-step').hide();

    // Show target step
    $('.reprint-step[data-step="' + stepNumber + '"]').show();

    // Update progress indicator
    $('.progress-step').removeClass('active completed');
    $('.progress-step').each(function () {
        var step = parseInt($(this).data('step'));
        if (step < stepNumber) {
            $(this).addClass('completed');
        } else if (step === stepNumber) {
            $(this).addClass('active');
        }
    });

    // Update buttons
    if (stepNumber === 1) {
        $('#btnBack').hide();
    } else {
        $('#btnBack').show();
    }

    if (stepNumber === 4) {
        $('#btnNext').hide();
        $('#btnCreate').show();
    } else {
        $('#btnNext').show();
        $('#btnCreate').hide();
    }

    ReprintWizard.currentStep = stepNumber;
}

// =============================================
// STEP 1: REPRINT TYPE SELECTION
// =============================================

function handleReprintTypeChange() {
    var selectedType = $('input[name="ReprintType"]:checked').val();
    ReprintWizard.formData.reprintType = selectedType;

    console.log('Reprint type changed:', selectedType);
}

function validateStep1() {
    if (!ReprintWizard.formData.reprintType) {
        showError('Please select a reprint type');
        return false;
    }
    return true;
}

// =============================================
// STEP 2: ITEM SELECTION (PARTIAL ONLY)
// =============================================

function loadJobItems() {
    var jobId = ReprintWizard.formData.originalJobId;

    $('#itemSelectionContainer').html('<p class="text-muted">Loading items...</p>');

    $.ajax({
        url: '/Reprint/GetJobItems',
        type: 'GET',
        data: { jobId: jobId },
        success: function (response) {
            if (response.success && response.items && response.items.length > 0) {
                ReprintWizard.jobItems = response.items;
                renderJobItems(response.items);
            } else {
                $('#itemSelectionContainer').html('<p class="text-warning">No items found for this job.</p>');
            }
        },
        error: function () {
            $('#itemSelectionContainer').html('<p class="text-danger">Error loading items.</p>');
        }
    });
}

function renderJobItems(items) {
    var html = '';

    items.forEach(function (item) {
        html += '<div class="item-checkbox-row">';
        html += '  <div class="form-check">';
        html += '    <input class="form-check-input item-selection-checkbox" type="checkbox" ';
        html += '           id="item_' + item.jobItemId + '" value="' + item.jobItemId + '" data-cost="' + item.cost + '">';
        html += '    <label class="form-check-label w-100" for="item_' + item.jobItemId + '">';
        html += '      <div class="d-flex justify-content-between">';
        html += '        <div>';
        html += '          <strong>' + (item.description || 'Untitled Item') + '</strong><br>';
        html += '          <small class="text-muted">Qty: ' + item.quantity + ' | Material: ' + item.material + '</small>';
        html += '        </div>';
        html += '        <div class="text-right">';
        html += '          <strong>' + item.formattedCost + '</strong>';
        html += '        </div>';
        html += '      </div>';
        html += '    </label>';
        html += '  </div>';
        html += '</div>';
    });

    $('#itemSelectionContainer').html(html);
}

function handleItemSelectionChange() {
    // Update selected items array
    ReprintWizard.formData.selectedItems = [];
    var selectedCost = 0;

    $('.item-selection-checkbox:checked').each(function () {
        var itemId = parseInt($(this).val());
        var itemCost = parseFloat($(this).data('cost')) || 0;

        ReprintWizard.formData.selectedItems.push(itemId);
        selectedCost += itemCost;

        // Visual feedback
        $(this).closest('.item-checkbox-row').addClass('selected');
    });

    $('.item-selection-checkbox:not(:checked)').each(function () {
        $(this).closest('.item-checkbox-row').removeClass('selected');
    });

    // Update cost display
    $('#selectedItemsCost').text('$' + selectedCost.toFixed(2));

    console.log('Selected items:', ReprintWizard.formData.selectedItems);
}

function validateStep2() {
    if (ReprintWizard.formData.reprintType === 'Partial') {
        if (ReprintWizard.formData.selectedItems.length === 0) {
            showError('Please select at least one item to reprint');
            return false;
        }
    }
    return true;
}

// =============================================
// STEP 3: REASON & COST ALLOCATION
// =============================================

function loadReasons() {
    $.ajax({
        url: '/Reprint/GetReasons',
        type: 'GET',
        success: function (response) {
            if (response.success && response.categories) {
                ReprintWizard.reasons = response.categories;
                renderReasons(response.categories);
            }
        },
        error: function () {
            console.error('Error loading reprint reasons');
        }
    });
}

function renderReasons(categories) {
    var select = $('#reprintReason');
    select.empty();
    select.append('<option value="">-- Select a reason --</option>');

    categories.forEach(function (category) {
        var optgroup = $('<optgroup label="' + category.category + '"></optgroup>');

        category.reasons.forEach(function (reason) {
            var option = $('<option></option>')
                .val(reason.reasonId)
                .text(reason.reasonText)
                .data('category', category.category)
                .data('default-allocation', reason.defaultCostAllocation);

            optgroup.append(option);
        });

        select.append(optgroup);
    });
}

function handleReasonChange() {
    var selectedOption = $('#reprintReason option:selected');
    var reasonId = selectedOption.val();
    var reasonText = selectedOption.text();
    var category = selectedOption.data('category');
    var defaultAllocation = selectedOption.data('default-allocation');

    ReprintWizard.formData.reprintReasonId = reasonId ? parseInt(reasonId) : null;
    ReprintWizard.formData.reprintReasonText = reasonText;
    ReprintWizard.formData.reprintCategory = category;

    // Auto-select default cost allocation if available
    if (defaultAllocation) {
        $('input[name="CostAllocation"][value="' + defaultAllocation + '"]').prop('checked', true);
        ReprintWizard.formData.costAllocation = defaultAllocation;
    }

    console.log('Reason changed:', reasonText, '| Category:', category);
}

function updateCostAllocationSummary() {
    var selectedAllocation = $('input[name="CostAllocation"]:checked').val();
    ReprintWizard.formData.costAllocation = selectedAllocation;

    console.log('Cost allocation changed:', selectedAllocation);
}

function validateStep3() {
    if (!ReprintWizard.formData.reprintReasonId) {
        showError('Please select a reprint reason');
        return false;
    }

    if (!ReprintWizard.formData.costAllocation) {
        showError('Please select who pays for this reprint');
        return false;
    }

    return true;
}

// =============================================
// STEP 4: DESCRIPTION & CONFIRMATION
// =============================================

function updateDescriptionCount() {
    var length = $('#description').val().length;
    $('#descriptionCount').text(length);

    if (length < 10) {
        $('#descriptionCount').addClass('text-danger');
    } else {
        $('#descriptionCount').removeClass('text-danger');
    }
}

function updateSummary() {
    // Type
    $('#summaryType').text(ReprintWizard.formData.reprintType);

    // Items (only for partial)
    if (ReprintWizard.formData.reprintType === 'Partial') {
        var itemCount = ReprintWizard.formData.selectedItems.length;
        $('#summaryItems').text(itemCount + ' item' + (itemCount !== 1 ? 's' : '') + ' selected');
        $('#summaryItemsRow').show();
    } else {
        $('#summaryItemsRow').hide();
    }

    // Reason
    $('#summaryReason').text(ReprintWizard.formData.reprintReasonText || '-');

    // Cost
    var costText = '$' + ReprintWizard.formData.estimatedCost.toFixed(2);
    if (ReprintWizard.formData.costAllocation) {
        costText += ' (' + ReprintWizard.formData.costAllocation + ')';
    }
    $('#summaryCost').text(costText);
}

function validateStep4() {
    var description = $('#description').val().trim();

    if (description.length < 10) {
        showError('Description must be at least 10 characters');
        return false;
    }

    if (description.length > 2000) {
        showError('Description cannot exceed 2000 characters');
        return false;
    }

    ReprintWizard.formData.description = description;
    ReprintWizard.formData.internalNotes = $('#internalNotes').val().trim();
    ReprintWizard.formData.notifyCustomer = $('#notifyCustomer').is(':checked');

    return true;
}

// =============================================
// VALIDATION
// =============================================

function validateCurrentStep() {
    switch (ReprintWizard.currentStep) {
        case 1:
            return validateStep1();
        case 2:
            return validateStep2();
        case 3:
            return validateStep3();
        case 4:
            return validateStep4();
        default:
            return true;
    }
}

// =============================================
// COST CALCULATION
// =============================================

function calculateEstimatedCost() {
    var jobId = ReprintWizard.formData.originalJobId;
    var selectedItemIds = ReprintWizard.formData.selectedItems;

    $.ajax({
        url: '/Reprint/CalculateCost',
        type: 'GET',
        data: {
            jobId: jobId,
            selectedItemIds: selectedItemIds
        },
        traditional: true, // Important for array serialization
        success: function (response) {
            if (response.success) {
                ReprintWizard.formData.estimatedCost = response.estimatedCost;
                $('#estimatedCost').text(response.formattedCost);
                console.log('Estimated cost:', response.formattedCost);
            } else {
                console.error('Error calculating cost:', response.message);
                ReprintWizard.formData.estimatedCost = 0;
                $('#estimatedCost').text('$0.00');
            }
        },
        error: function () {
            console.error('Error calculating cost');
            ReprintWizard.formData.estimatedCost = 0;
            $('#estimatedCost').text('$0.00');
        }
    });
}

// =============================================
// SUBMISSION
// =============================================

function handleSubmitReprint() {
    // Final validation
    if (!validateCurrentStep()) {
        return;
    }

    // Disable button and show loading
    $('#btnCreate').prop('disabled', true).html('<i class="fa fa-spinner fa-spin"></i> Creating...');

    // Prepare request data
    var requestData = {
        OriginalJobId: ReprintWizard.formData.originalJobId,
        ReprintType: ReprintWizard.formData.reprintType,
        SelectedItemIds: ReprintWizard.formData.selectedItems,
        ReprintReasonId: ReprintWizard.formData.reprintReasonId,
        ReprintCategory: ReprintWizard.formData.reprintCategory,
        ReprintReasonText: ReprintWizard.formData.reprintReasonText,
        CostAllocation: ReprintWizard.formData.costAllocation,
        EstimatedCost: ReprintWizard.formData.estimatedCost,
        Description: ReprintWizard.formData.description,
        InternalNotes: ReprintWizard.formData.internalNotes,
        NotifyCustomer: ReprintWizard.formData.notifyCustomer
    };

    console.log('Submitting reprint request:', requestData);

    // Submit to server
    $.ajax({
        url: '/Reprint/Create',
        type: 'POST',
        data: JSON.stringify(requestData),
        contentType: 'application/json',
        success: function (response) {
            if (response.success) {
                showSuccess('Reprint job #' + response.jobNumber + ' created successfully! Redirecting to edit page...');
                $('#reprintModal').modal('hide');

                // Redirect to edit page so user can update quantities
                setTimeout(function () {
                    window.location.href = '/Job/EditJob?jobId=' + response.jobId;
                }, 1500);
            } else {
                showError('Failed to create reprint: ' + response.message);
                $('#btnCreate').prop('disabled', false).html('<i class="fa fa-check"></i> Create Reprint');
            }
        },
        error: function (xhr, status, error) {
            console.error('Error creating reprint:', error);
            showError('An error occurred while creating the reprint job. Please try again.');
            $('#btnCreate').prop('disabled', false).html('<i class="fa fa-check"></i> Create Reprint');
        }
    });
}

// =============================================
// RESET & CLEANUP
// =============================================

function resetModal() {
    // Reset wizard state
    ReprintWizard.currentStep = 1;
    ReprintWizard.formData = {
        originalJobId: null,
        originalJobNumber: null,
        reprintType: null,
        selectedItems: [],
        reprintReasonId: null,
        reprintReasonText: null,
        reprintCategory: null,
        costAllocation: null,
        estimatedCost: 0,
        description: null,
        internalNotes: null,
        notifyCustomer: false
    };
    ReprintWizard.jobItems = [];

    // Reset form
    $('#reprintForm')[0].reset();
    $('#descriptionCount').text('0');

    // Reset visual states
    $('.item-checkbox-row').removeClass('selected');
    $('input[name="ReprintType"]').prop('checked', false);
    $('input[name="CostAllocation"]').prop('checked', false);

    // Go back to step 1
    goToStep(1);
}

// =============================================
// UI HELPERS
// =============================================

function showSuccess(message) {
    // Using toastr if available, otherwise alert
    if (typeof toastr !== 'undefined') {
        toastr.success(message);
    } else {
        alert(message);
    }
}

function showError(message) {
    // Using toastr if available, otherwise alert
    if (typeof toastr !== 'undefined') {
        toastr.error(message);
    } else {
        alert('Error: ' + message);
    }
}

function showWarning(message) {
    // Using toastr if available, otherwise alert
    if (typeof toastr !== 'undefined') {
        toastr.warning(message);
    } else {
        alert('Warning: ' + message);
    }
}
