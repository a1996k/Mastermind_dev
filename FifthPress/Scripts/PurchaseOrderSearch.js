let $vendorMSL = $('.Vendors-msl-dropdown-container .msl-dropdown-list');
let $creatorMSL = $('.Users-msl-dropdown-container .msl-dropdown-list');

const $resultsTable = $('#results_table');

function ShowResultsTable() {
    $resultsTable.fadeIn('fast');
};

function HideResultsTable() {
    $resultsTable.fadeOut('fast');
};

function GetKeywordTerms() {
    let termsArray = [];
    $('.search-term').each(function () {
        let term = $(this).find('input').val();
        if (term) {
            termsArray.push(term);
        }
    });
    return termsArray.join(' ');
}

function VendorTextProvided() {
    return ($('#partial_vendor_name').val() !== '');
};

function GetVendorFilterValue() {
    if (!VendorTextProvided()) {
        if (!AllVendorsSelected()) {
            return $vendorMSL.find('input:checked').map(function () {
                return $(this).attr('value');
            }).get().join(',');
        }
    } else {
        return $('#partial_vendor_name').val();
    }
    return null;
};

function GetCreatedByFilterValue() {
    if (!AllCreatorsSelected()) {
        return $creatorMSL.find('input:checked').map(function () {
            return $(this).attr('value');
        }).get().join(',');
    }
    return null;
};

function AllVendorsSelected() {
    return $vendorMSL.find('li input:checked').length === $vendorMSL.find('li').length;
};

function AllCreatorsSelected() {
    return $creatorMSL.find('li input:checked').length === $creatorMSL.find('li').length;
};

function DeInitResultsTable() {
    if ($.fn.DataTable.isDataTable('#results_table')) {
        $resultsTable.DataTable().destroy();
        $resultsTable.find('tbody').empty();
    }
};

function InitResultsTable() {
    $resultsTable.DataTable({
        "order": [[0, "desc"]],
        "columnDefs": [
            {
                "targets": [7],
                "render": function (data, type) {
                    var date = moment(data, "dddd, MMMM D, YYYY", true);
                    return type === 'display' || type === 'filter'
                        ? (date.isValid() ? date.format('dddd, MMMM D, YYYY') : '')
                        : (date.isValid() ? date.toISOString() : '');
                }
            }
        ]
    });
    ShowResultsTable();
};

function ClearResultsTable() {
    DeInitResultsTable();
    HideResultsTable();
};

function UpdateResultsTable(results) {
    ClearResultsTable();
    for (let i = 0; i < results.length; i++) {
        let aUrl = ProcessUrl('PurchaseOrder/Edit') + `/${results[i].POId}`;
        const rowHtml = `
            <tr>
                <td>${results[i].FormattedPONumber}</td>
                <td>${results[i].JobNumber ?? ''}</td>
                <td>${results[i].VendorName}</td>
                <td>${results[i].ItemTitle}</td>
                <td>${results[i].StatusLabel}</td>
                <td>${results[i].RequestorName}</td>
                <td>${results[i].ApproverName}</td>
                <td>${results[i].ApprovalDateString ?? ''}</td>
                <td>$${Number(results[i].TotalPrice ?? 0).toFixed(2)}</td>
                <td><a href="${aUrl}">Go To</a></td>
            </tr>`;
        $resultsTable.find('tbody').append(rowHtml);
    }
    InitResultsTable();
};

function GetStatusFilterValue() {
    return $('input[name="POStatus"]:checked').map(function () {
        return $(this).val();
    }).get().join(',');
}

function SearchSubmit() {
    const searchModel = GetSearchModel();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('purchase-order-search'),
        data: { searchModel },
        success: function (data) {
            try {
                const res = JSON.parse(data);
                UpdateResultsTable(res);
            } catch (e) { }
        }
    });
};

function GetSearchModel() {
    const model = {
        PONumber: $('#po_number').val(),
        Vendor: GetVendorFilterValue(),
        Status: GetStatusFilterValue(),
        CreatedBy: GetCreatedByFilterValue(),
        Keywords: GetKeywordTerms()
    };

    if ($('#enable_df').is(':checked')) {
        const rangeval = $('#range_filter').val();
        const rangeSplit = rangeval.split('-');
        model.DateFrom = rangeSplit[0].trim();
        model.DateTo = rangeSplit[1].trim();
    }

    if ($('#enable_due_df').is(':checked')) {
        const dueval = $('#due_range_filter').val();
        const dueSplit = dueval.split('-');
        model.DueFrom = dueSplit[0].trim();
        model.DueTo = dueSplit[1].trim();
    }

    console.log(model);
    return model;
};

function ToggleVendorFilter() {
    const isPartial = $(this).attr('id') === 'vendor_pn_radio';
    $('#partial_vendor_name').prop('disabled', !isPartial);
    $('.Vendors-Dummy').prop('disabled', isPartial);
    if (!isPartial) $('#partial_vendor_name').val('');
};

function ResetSearchForm() {
    $('#po_number').val('');
    $('#vendor_sl_radio').trigger('click');
    $vendorMSL.find('input[type="checkbox"]').prop('checked', true);
    $('#msl_all_cb').prop('checked', true);
    $('input[name="POStatus"]').prop('checked', false);
    $creatorMSL.find('input[type="checkbox"]').prop('checked', true);
    $('#range_filter').val('');
    $('#enable_df').prop('checked', false);
    $('#range_filter').prop('disabled', true);
    $('#due_range_filter').val('');
    $('#enable_due_df').prop('checked', false);
    $('#due_range_filter').prop('disabled', true);
    $('.search-term .remove-me').each(function () { $(this).trigger('click'); });
};

function NewPO() {
    window.location = ProcessUrl('PurchaseOrders/Create');
};

$(function () {
    $(document).on('click', function (event) {
        if (!$(event.target).closest('.result-actions').length && !$(event.target).closest('#po_context_menu').length) {
            $('#po_context_menu').hide();
        }
    });

    $('input[type="radio"]:checked').each(function () {
        $(this).trigger('click');
    });

    $('.add-search-term').trigger('click');
    $('#po_number').focus();
})
    .on('click', '.submit-search-btn', SearchSubmit)
    .on('click', '.add-search-term', function () {
        $('.search-terms').append(`
            <li class="search-term d-flex justify-content-between">
                <input type="text" class="styled-input ff-dm-sans fs-12 w-100" />
                <span class="remove-me ms-3"> <span class="minus-sign">-</span> </span>
            </li>
        `);
        $('.search-terms li:last-child input').trigger('focus');
    })
    .on('click', '.remove-me', function () {
        $(this).parents('.search-term').remove();
    })
    .on('click', 'input[name="vendorSLRadio"]', ToggleVendorFilter)
    .on('click', '.reset-search', ResetSearchForm)
    .on('click', '.new-po-btn', NewPO)
    .on('click', '#enable_df', function () {
        if ($(this).is(':checked')) InitDatePicker();
        else DestroyDatePicker();
    })
    .on('click', '#enable_due_df', function () {
        if ($(this).is(':checked')) InitDueDatePicker();
        else DestroyDueDatePicker();
    });

function InitDatePicker() {
    $('#range_filter').daterangepicker({
        opens: 'left',
        ranges: {
            'Today': [moment(), moment()],
            'Yesterday': [moment().subtract(1, 'days'), moment().subtract(1, 'days')],
            'Last 7 Days': [moment().subtract(6, 'days'), moment()],
            'Last 30 Days': [moment().subtract(29, 'days'), moment()],
            'This Month': [moment().startOf('month'), moment().endOf('month')],
            'Last Month': [moment().subtract(1, 'month').startOf('month'), moment().subtract(1, 'month').endOf('month')]
        },
    });
    $('#range_filter').prop('disabled', false);
};

function DestroyDatePicker() {
    $('#range_filter').daterangepicker("destroy");
    $('#range_filter').val('').prop('disabled', true);
};

function InitDueDatePicker() {
    $('#due_range_filter').daterangepicker({
        opens: 'left',
        ranges: {
            'Today': [moment(), moment()],
            'Last 7 Days': [moment().subtract(6, 'days'), moment()],
            'Last 30 Days': [moment().subtract(29, 'days'), moment()]
        },
    });
    $('#due_range_filter').prop('disabled', false);
};

function DestroyDueDatePicker() {
    $('#due_range_filter').daterangepicker("destroy");
    $('#due_range_filter').val('').prop('disabled', true);
};