var breakdown = new Array();
var timing = new Array();
var newBreakdown = new Array();
var newBreakdownTotals = new Array();
var overallTotal;

function HasValidDuration(el) {
    let validDuration =
        typeof el !== 'undefined' &&
        el !== null &&
        el !== "0.00" &&
        el != "0" &&
        el != 0;

    return validDuration;
}

/* end pricing ui functions */

/* pricing logic functions */

function PerformCalcs() {
    let recordtype = '';
    let url = window.location.href;
    var isQuote = url.toLowerCase().includes('quote') || url.includes('-quote-');
    var isJob = url.toLowerCase().includes('job') || url.includes('-job-');
    let recordId = -1;

    newBreakdown = new Array();

    recordId = $('#Id').val();

    if (isQuote) {
        recordtype = "QUOTE";

        if (typeof recordId === 'undefined') {
            recordId = $('#VM_Quote_Quote_Id').val();
        }
    }

    if (isJob) {
        recordtype = "JOB";

        if (typeof recordId === 'undefined') {
            recordId = $('#VM_Job_Job_Id').val();
        }
    }

    $.ajax({
        type: 'POST',
        url: ProcessUrl('run-calcs-alt'),
        data: {
            recordtype: recordtype,
            recordId: recordId
        },
        beforeSend: ShowCalcSpinner,
        success: function (data) {
            try {
                let res = JSON.parse(data);

                // Display calculation errors if any
                $('#calc_error_modal').remove();
                if (res.calcErrors && res.calcErrors.length > 0) {
                    var errorList = '';
                    for (var ei = 0; ei < res.calcErrors.length; ei++) {
                        errorList += '<li>' + $('<span>').text(res.calcErrors[ei]).html() + '</li>';
                    }
                    var modalHtml =
                        '<div class="modal fade" id="calc_error_modal" tabindex="-1">' +
                            '<div class="modal-dialog">' +
                                '<div class="modal-content">' +
                                    '<div class="modal-header bg-danger text-white">' +
                                        '<h5 class="modal-title"><i class="fa-solid fa-triangle-exclamation"></i> Calculation Errors</h5>' +
                                        '<button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal"></button>' +
                                    '</div>' +
                                    '<div class="modal-body">' +
                                        '<p>The following errors occurred during calculation:</p>' +
                                        '<ul>' + errorList + '</ul>' +
                                    '</div>' +
                                    '<div class="modal-footer">' +
                                        '<button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>' +
                                    '</div>' +
                                '</div>' +
                            '</div>' +
                        '</div>';
                    $('body').append(modalHtml);
                    new bootstrap.Modal(document.getElementById('calc_error_modal')).show();
                }

                //reset breakdown array
                newBreakdown = new Array();
                overallTotal = res.overallTotal;

                console.log(res.totalPrices);

                //new pricing list html here
                //remove the old pricing list modal
                $('.body-content #priceListModal').remove();

                //insert the new pricing list modal
                $('.body-content').append(res.newPricingList);

                //update displayed Item prices
                $('.price').html('');

                // Build a lookup of overridden item IDs from additionalData
                var overriddenItems = {};
                var serviceMarkupOverrideItems = {};
                if (res.additionalData) {
                    for (let a = 0; a < res.additionalData.length; a++) {
                        if (res.additionalData[a].OverriddenPrice != null) {
                            overriddenItems[res.additionalData[a].Id] = true;
                        }
                        if (res.additionalData[a].HasServiceMarkupOverride) {
                            serviceMarkupOverrideItems[res.additionalData[a].Id] = true;
                        }
                    }
                }

                for (let i = 0; i < res.totalPrices.length; i++) {
                    let thisItemId = res.totalPrices[i].JobItem;
                    if (recordtype === 'QUOTE') {
                        thisItemId = res.totalPrices[i].QuoteItem;
                    }

                    let isOverridden = overriddenItems[thisItemId] === true;
                    let hasServiceOverride = serviceMarkupOverrideItems[thisItemId] === true;
                    let revertDisplay = isOverridden ? 'inline' : 'none';
                    let serviceOverrideDisplay = hasServiceOverride ? 'inline' : 'none';
                    let priceVal = res.totalPrices[i].Total_Price || 0;

                    let html =
                        `
                            <div class="price-entry" data-jump-to-id="${thisItemId}" data-item-id="${thisItemId}">
                                <span class="ff-dm-sans">Item <span class="ff-lato">${res.totalPrices[i].Ordinal}</span>:</span>
                                <span class="item-price-display ff-lato" style="cursor:pointer;" title="Click to override price">$${res.totalPrices[i].Total_Price}</span>
                                <input type="number" class="item-price-input styled-input" style="display:none; width:100px;" step="0.01" value="${priceVal}" />
                                <i class="fa-solid fa-arrow-rotate-left revert-price-icon" title="Clear manual override" style="display:${revertDisplay}; cursor:pointer; margin-left:4px;"></i>
                                <i class="fa-solid fa-pen markup-override-icon" title="Service markup adjusted in Cost Comparison" style="display:${serviceOverrideDisplay}; color:orange; margin-left:4px;"></i>
                            </div>
                        `;

                    $('.price').append(html);
                }

                //end update displayed item prices
                //update displayed total price
                //res.overallTotal
                $('.total').html('');
                let totalHtml =
                    `
                    <div class="field-binder-row-flex justify-content-between">
                        <span class="ff-dm-sans fs-16 d-flex flex-row justify-content-between w-100">Total: <span>
                        <span class="ff-lato fs-16">$${res.overallTotal}</span></span>
                    </div>
                    `;

                $('.total').html(totalHtml);
                //end update displayed total price

                // Update Client Cost Splits with new total
                if (typeof ClientCostSplits !== 'undefined' && typeof ClientCostSplits.updateTotalAmount === 'function') {
                    var newTotal = parseFloat(res.overallTotal.toString().replace(/,/g, '')) || 0;
                    ClientCostSplits.updateTotalAmount(newTotal);
                }

                // Handle sidebar markup based on whether any items have manual overrides
                var hasOverrides = Object.keys(overriddenItems).length > 0;
                if (hasOverrides) {
                    // Show effective markup and disable the input
                    if (res.overallCost && res.overallCost > 0) {
                        var effectiveMarkup = ((res.overallTotal - res.overallCost) / res.overallCost) * 100;
                        $('.markup-input').val(effectiveMarkup.toFixed(2));
                    }
                    $('.markup-input').prop('disabled', true).attr('title', 'Clear manual item overrides to use job-level markup');
                } else {
                    $('.markup-input').prop('disabled', false).attr('title', '');
                }

                for (let i = 0; i < res.additionalData.length; i++) {
                    var $row = $('.item-row[data-record-id="' + res.additionalData[i].Id + '"]');

                    $row.find('.stock-row').find('.run-size').html(res.additionalData[i].RunLength + ' x ' + res.additionalData[i].RunWidth);
                    $row.find('.calced-up-val').html(res.additionalData[i].Up);
                    $row.find('.total-sheets').html(res.additionalData[i].Sheets);

                    if (isQuote) {
                        $row.find('#QuoteItem_RunLength').val(res.additionalData[i].RunLength);
                        $row.find('#QuoteItem_RunWidth').val(res.additionalData[i].RunWidth);
                    }
                    else {
                        $row.find('#JobItem_RunLength').val(res.additionalData[i].RunLength);
                        $row.find('#JobItem_RunWidth').val(res.additionalData[i].RunWidth);
                    }
                    $row.find('.sheets-display').html(res.additionalData[i].Sheets);
                    $row.find('.buy-sheets-display').html(res.additionalData[i].BuySheets);
                    $row.find('.scrap-sheets-display').html(res.additionalData[i].TotalScrapSheets);
                    $row.find('.run-sheets-display').html(res.additionalData[i].RunSheets);
                    $row.find('.out-display').html(res.additionalData[i].Out);
                    $row.find('.front-color-display').html(res.additionalData[i].PressColorProfileFront);
                    $row.find('.back-color-display').html(res.additionalData[i].PressColorProfileBack);

                    ReRenderRowServices(res.additionalData[i].Id);
                }
            } catch (e) {
                console.log(e);
            }
        },
        complete: HideCalcSpinner
    });
};

function JumpToItem(e) {
    // Don't scroll if user clicked on price edit controls
    var $clicked = $(e.target);
    if ($clicked.hasClass('item-price-display') ||
        $clicked.hasClass('item-price-input') ||
        $clicked.hasClass('revert-price-icon') ||
        $clicked.hasClass('markup-override-icon')) {
        return;
    }

    let rId = $(this).attr('data-jump-to-id');
    let $tgt = $(`.item-row[data-record-id="${rId}"]`);

    $('html, body').animate({
        scrollTop: $tgt.offset().top - 300
    }, 1000);
};

//new
function SubmitMarkupOverride(evt) {
    let $input = $(this);
    //let isItemTotal = $input.parents('li').hasClass('total');
    let isItemTotal = $input.hasClass('group-markup');

    let recType2 = 'quote';

    if (window.location.href.indexOf('Job') > -1) {
        recType2 = "job";
    }

    if (evt.keyCode === 13) {
        //get the id and the new value
        let rId = $input.attr('data-record-id');

        if (isItemTotal) {
            rId = $input.attr('data-jobitem-id');
        }

        let displayVal = parseFloat($input.val()) || 0;
        let val = displayVal / 100; // Convert percentage to ratio for storage

        //ajax to back end

        $.ajax({
            type: 'POST',
            url: ProcessUrl('override-specific-markup'),
            data: {
                recordId: rId,
                val,
                recType2,
                isItemTotal
            },
            success: function () {
                if (isItemTotal) {
                    // Update all individual markup inputs for this item to show the same percentage
                    $('.markup-val-2[data-jobitem-id="' + rId + '"]').val(displayVal.toFixed(2));
                    // Highlight all rows for this item and add reset icons
                    var $rows = $('tr[data-jobitem-id="' + rId + '"]');
                    $rows.addClass('markup-overridden');
                    $rows.each(function () {
                        if ($(this).find('.reset-service-markup').length === 0) {
                            $(this).find('.markup-val-2').after(
                                '<i class="fa-solid fa-arrow-rotate-left reset-service-markup" data-record-id="' +
                                $(this).attr('data-record-id') +
                                '" title="Reset to default markup" style="cursor:pointer; margin-left:4px; color:#856404;"></i>'
                            );
                        }
                    });
                } else {
                    // Single service row: highlight and add reset icon
                    var $row = $input.closest('tr');
                    $row.addClass('markup-overridden');
                    if ($row.find('.reset-service-markup').length === 0) {
                        $input.after(
                            '<i class="fa-solid fa-arrow-rotate-left reset-service-markup" data-record-id="' +
                            rId +
                            '" title="Reset to default markup" style="cursor:pointer; margin-left:4px; color:#856404;"></i>'
                        );
                    }
                }
            }
        });
    }
};

function EnablePriceEdit(e) {
    e.stopPropagation(); // Prevent JumpToItem from firing

    let $entry = $(this).closest('.price-entry');
    let $display = $entry.find('.item-price-display');
    let $input = $entry.find('.item-price-input');

    // Parse current price from display text (remove $ and commas)
    let currentPrice = parseFloat($display.text().replace(/[$,]/g, '')) || 0;
    $input.val(currentPrice);

    $display.hide();
    $input.show().focus().select();
};

function OverrideItemPrice(evt) {
    if (evt.keyCode === 27) {
        // Escape - cancel edit
        let $entry = $(this).closest('.price-entry');
        $entry.find('.item-price-input').hide();
        $entry.find('.item-price-display').show();
        return;
    }

    if (evt.keyCode !== 13) return;

    let $input = $(this);
    let $entry = $input.closest('.price-entry');
    let itemId = $entry.attr('data-item-id');
    let newPrice = parseFloat($input.val());

    if (isNaN(newPrice)) return;

    // Show spinner immediately for user feedback
    ShowCalcSpinner();

    let url = window.location.href;
    let recordType = url.toLowerCase().includes('quote') || url.includes('-quote-') ? 'QUOTE' : 'JOB';

    $.ajax({
        type: 'POST',
        url: ProcessUrl('override-item-price'),
        data: {
            recordType: recordType,
            itemId: itemId,
            newPrice: newPrice
        },
        success: function (response) {
            let res = typeof response === 'string' ? JSON.parse(response) : response;
            if (res.error) {
                console.error(res.error);
                HideCalcSpinner();
                return;
            }
            // Re-run calculations to refresh all prices (PerformCalcs manages its own spinner)
            PerformCalcs();
        },
        error: function (xhr, status, error) {
            console.error('Error overriding price:', error);
            HideCalcSpinner();
        }
    });
};

function RevertItemPrice(e) {
    e.stopPropagation(); // Prevent JumpToItem from firing
    let $entry = $(this).closest('.price-entry');
    let itemId = $entry.attr('data-item-id');

    // Show spinner immediately for user feedback
    ShowCalcSpinner();

    let url = window.location.href;
    let recordType = url.toLowerCase().includes('quote') || url.includes('-quote-') ? 'QUOTE' : 'JOB';

    $.ajax({
        type: 'POST',
        url: ProcessUrl('revert-item-price'),
        data: {
            recordType: recordType,
            itemId: itemId
        },
        success: function (response) {
            let res = typeof response === 'string' ? JSON.parse(response) : response;
            if (res.error) {
                console.error(res.error);
                HideCalcSpinner();
                return;
            }
            PerformCalcs();
        },
        error: function (xhr, status, error) {
            console.error('Error clearing override:', error);
            HideCalcSpinner();
        }
    });
};

function ResetServiceMarkup(e) {
    e.stopPropagation();
    let $icon = $(this);
    let recordId = $icon.attr('data-record-id');
    let $row = $icon.closest('tr');

    let url = window.location.href;
    let recType = url.toLowerCase().includes('quote') || url.includes('-quote-') ? 'QUOTE' : 'JOB';

    $.ajax({
        type: 'POST',
        url: ProcessUrl('reset-service-markup'),
        data: {
            recordId: recordId,
            recType: recType
        },
        success: function (response) {
            let res = typeof response === 'string' ? JSON.parse(response) : response;
            if (res.error) {
                console.error(res.error);
                return;
            }
            // Remove highlight and reset icon from the row
            $row.removeClass('markup-overridden');
            $row.find('.reset-service-markup').remove();
            // Set the markup input back to the overall job/quote markup value
            var defaultMarkup = $('.markup-input').val();
            $row.find('.markup-val-2').val(defaultMarkup ? parseFloat(defaultMarkup).toFixed(2) : '');
        },
        error: function (xhr, status, error) {
            console.error('Error resetting service markup:', error);
        }
    });
};

/*end pricing logic functions */

function InitPriceTable() {
    $('#priceListModal .table').each(function () {
        let $t = $(this);
        // Initialize DataTable if it isn't already initialized.
        if (!$.fn.DataTable.isDataTable($t)) {
            $t.DataTable({
                lengthChange: false,
                paging: false,
                order: [[6, 'asc']]
            });
        }
    });
};

function DeInitPriceTable() {
    $('#priceListModal .table').each(function () {
        let $t = $(this);
        // Destroy the DataTable if it's been initialized.
        if ($.fn.DataTable.isDataTable($t)) {
            // The destroy() method removes the DataTables functionality.
            // Optionally, pass true to also remove the DataTables-added elements.
            $t.DataTable().destroy();
        }
    });
};

$(function () {
    //if we're on a page with Quote or Job in the url, show the pricing wrapper
    // Sample URL
    const url = window.location.href;

    // Regular expression pattern with the 'i' flag for case-insensitivity
    const pattern = /job|quote/i;

    if (pattern.test(url) && $('.error').length === 0) {
        $('.pricing-wrapper').removeClass('d-none');
    }
})
    .on('keyup', 'input.markup-val-2', SubmitMarkupOverride)
    .on('click', 'div[data-jump-to-id]', JumpToItem)
    .on('show.bs.modal', '#priceListModal', InitPriceTable)
    .on('hide.bs.modal', '#priceListModal', DeInitPriceTable)
    .on('click', '.item-price-display', EnablePriceEdit)
    .on('keyup', 'input.item-price-input', OverrideItemPrice)
    .on('click', '.revert-price-icon', RevertItemPrice)
    .on('click', '.reset-service-markup', ResetServiceMarkup)
    .on('blur', 'input.item-price-input', function () {
        // Cancel edit on blur - revert to display mode
        let $entry = $(this).closest('.price-entry');
        $(this).hide();
        $entry.find('.item-price-display').show();
    });