let $clientMSL = $('.Clients-msl-dropdown-container .msl-dropdown-list');
let $userMSL = $('.Users-msl-dropdown-container .msl-dropdown-list');

let rangeStart, rangeEnd;

const $contextMenu = $('#job_context_menu');

function ShowResultsTable() {
    $('#results_table').fadeIn('fast');
};

function HideResultsTable() {
    $('#results_table').fadeOut('fast');
};

function GetKeywordTerms() {
    let termsArray = [];

    $('.search-term').each(function () {
        let $item = $(this);
        let term = $item.find('input').val();
        if (term) {
            termsArray.push(term);
        }
    });

    return termsArray.join(',');
}

function ClientTextProvided() {
    //return if partial client name does not equal empty string
    return ($('#partial_client_name').val() !== '');
};

function GetClientFilterValue() {
    let filterValue;

    //if we haven't provided client text...
    if (!ClientTextProvided()) {
        //...check to see if all clients are selected
        if (!AllClientsSelected()) {
            //get all client ids and comma join them; return the value
            filterValue = $clientMSL.find('input:checked').map(function () {
                return $(this).attr('value');
            }).get().join(',');
        }
    }
    else {
        //get the entered client text
        filterValue = $('#partial_client_name').val();
    }

    return filterValue;
};

function GetUserFilterValue() {
    let filterValue;

    if (!AllUsersSelected()) {
        filterValue = $userMSL.find('input:checked').map(function () {
            return $(this).attr('value');
        }).get().join(',');
    }

    return filterValue;
};

function AllClientsSelected() {
    let checkedCount = $clientMSL.find('li input:checked').length;
    let totalCount = $clientMSL.find('li').length;
    return (checkedCount === totalCount);
};

function AllUsersSelected() {
    let checkedCount = $userMSL.find('li input:checked').length;
    let totalCount = $userMSL.find('li').length;

    return (checkedCount === totalCount);
};

function DeInitResultsTable() {
    if ($.fn.DataTable.isDataTable('#results_table')) {
        $('#results_table').DataTable().destroy();
        $('#results_table tbody').empty();
    }
};
function InitResultsTable() {
    $('#results_table').DataTable({
        "order": [[0, "desc"]], // Sort by the first column (index 0) in descending order
        "columnDefs": [
            {
                "targets": [5, 6], // Date Created and Date Completed columns
                "render": function (data, type, row) {
                    // Adjust the date format to match the format in your table
                    var date = moment(data, "dddd, MMMM D, YYYY", true);

                    if (type === 'display' || type === 'filter') {
                        if (!date.isValid()) {
                            return ''; // Custom message for invalid date
                        }
                        return date.format('dddd, MMMM D, YYYY'); // Display format
                    }

                    // Use ISO format for sorting
                    return date.isValid() ? date.toISOString() : ''; // ISO format ensures proper sorting
                }
            }
        ]
    });

    ShowResultsTable();
};
function ClearResultsTable() {
    DeInitResultsTable();
    HideResultsTable();

    $('#results_table tbody').html('');
};

function UpdateResultsTable(recs) {
    let theRecs = recs;
    let isJob = $('#job_ref').is(':checked');
    let editUrlPrefix = ProcessUrl(isJob ? 'Job/Edit/' : 'Quote/Edit/');

    ClearResultsTable();

    // Build entire tbody markup in one string and write it in a single DOM
    // mutation. Per-row .append() forces a reflow every iteration, which
    // hangs the browser ~1 min on 10k+ result sets.
    const rowsHtml = new Array(theRecs.length);
    for (var i = 0; i < theRecs.length; i++) {
        const rec = theRecs[i];
        const cancelledBadge = rec.IsCancelled
            ? ` <span class="badge bg-danger text-white ff-dm-sans fs-12">Cancelled</span>`
            : '';
        rowsHtml[i] =
            `
                    <tr data-record-id="${rec.ParentId}"${rec.IsCancelled ? ' class="record-cancelled"' : ''}>
                        <td><a href="${editUrlPrefix}${rec.ParentId}">${rec.ReferenceNumber}</a>${cancelledBadge}</td>
                        <td class="field-binder-col-flex"><span>${rec.ClientTitle}</span><span class="ff-dm-sans fs-14 fw-normal">${rec.Description}</span></td>
                        <td>${rec.Dimensions}</td>
                        <td>${rec.TotalQuantity}</td>
                        <td>${rec.TotalPriceString}</td>
                        <td>${rec.CreatedString}</td>
                        <td>${rec.CompletedString}</td>
                        <td>${rec.ProjectManager}</td>
                        <td class="result-actions">Actions</td>
                    </tr>
                `;
    }
    document.querySelector('#results_table tbody').innerHTML = rowsHtml.join('');

    InitResultsTable();
};

function SearchSubmit(event) {
    const searchModel = GetSearchModel();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('record-search'),
        data: {
            searchModel
        },
        success: function (data) {
            try {
                let res = JSON.parse(data);

                UpdateResultsTable(res);
            } catch (e) { }
        }
    });
};

function AddSearchTerm() {
    let html =
        `
        <li class="search-term d-flex justify-content-between">
            <input type="text" class="styled-input ff-dm-sans fs-12 w-100" />
            <span class="remove-me ms-3"> <span class="minus-sign">-</span> </span>
        </li>
        `;

    $('.search-terms').append(html);

    $('.search-terms li:last-child input').trigger('focus');
};

function RemoveSearchTerm() {
    let $term = $(this).parents('.search-term');
    $term.remove();
};

function HideMSL() {
    //hide any and all open msl's
    $('.msl-dropdown-container').each(function () {
        $(this).hide();
    });
};

function ToggleClientFilter() {
    let $btn = $(this);
    if ($btn.attr('id') === 'client_pn_radio') {
        //disable select
        $('.Clients-Dummy').prop('disabled', true);
        //enable input
        $('#partial_client_name').prop('disabled', false);
    } else {
        //disable input
        $('#partial_client_name').prop('disabled', true);
        //clear input
        $('#partial_client_name').val('');
        //enable select
        $('.Clients-Dummy').prop('disabled', false);
    }
};

function ToggleOnlyMyJobs() {
    let $cb = $(this);
    let checked = $cb.is(':checked');

    let $UsersList = $('.Users-msl-dropdown-container .msl-dropdown-list');

    if (checked) {
        let uId = getCookie('ActiveUser');
        $UsersList.find('li input').prop('checked', false);
        $UsersList.find('li input[value="' + uId + '"]').prop('checked', true);
    } else {
        $UsersList.find('li input').prop('checked', true);
    }
};

function ResetSearchForm() {
    //select job radio ref
    $('#job_ref').trigger('click');
    //clear job/quote #
    $('#job_quote').val('');
    //activate client select
    $('#client_sl_radio').trigger('click');
    //check all clients
    $clientMSL.find('input[type="checkbox"]').prop('checked', true);
    //ensure all "all" checkboxes are checked in the msl
    $('#msl_all_cb').prop('checked', true);
    //check all users
    //$userMSL.find('input[type="checkbox"]').prop('checked', true);
    //clear job statuses
    $('input[type="checkbox"][name="JobStatus"]').prop('checked', false);
    //clear date range
    $('#range_filter').val('');
    //uncheck only show my jobs (will take care of selecting all users)
    //click once - if checked, click again
    $('#only_my_jobs').trigger('click');
    if ($('#only_my_jobs').is(':checked')) {
        $('#only_my_jobs').trigger('click');
    }
    $('#enable_df').trigger('click');
    if ($('#enable_df').is(':checked')) {
        $('#enable_df').trigger('click');
    }
    //select "Created" radio
    $('#created_dateref').trigger('click');
    //reset dimensions
    $('#length').val('');
    $('#width').val('');
    //clear keyworks
    $('.search-term .remove-me').each(function () {
        $(this).trigger('click');
    });
};

function GetSearchModel() {
    let model = {};

    model.SearchJobs = $('#job_ref').is(':checked');
    model.EnteredToday = $('#entered_today').is(':checked');
    model.WaitingOnArt = $('#waiting_on_art').is(':checked');
    model.InProgress = $('#in_progress').is(':checked');
    model.ReadyToShip = $('#ready_to_ship').is(':checked');
    model.ReadyToBill = $('#ready_to_bill').is(':checked');
    model.AtPrebill = $('#at_prebill').is(':checked');
    model.IsHistory = $('#is_history').is(':checked');
    model.CreatedAtDateType = $('#created_dateref').is(':checked');
    model.JobNumber = $('#job_quote').val();
    model.Client = GetClientFilterValue();
    model.AssignedTo = GetUserFilterValue();
    /*model.DateFrom = $('#start_date').val();
    model.DateTo = $('#end_date').val();*/
    model.Length = $('#length').val();
    model.Width = $('#width').val();
    model.Keywords = GetKeywordTerms();

    if ($('#enable_df').is(':checked')) {
        let rangeval = $('#range_filter').val();
        let rangeSplit = rangeval.split('-');
        let from = rangeSplit[0].trim();
        let to = rangeSplit[1].trim();
        model.DateFrom = from;
        model.DateTo = to;
    }
    console.log(model);
    return model;
};

function showContextMenu(event, $jobElement) {
    const offset = $jobElement.offset();
    const rId = $jobElement.parents('tr').attr('data-record-id');
    const isJob = $('#job_ref').is(':checked');

    $('#qa_selected_job_id').val(rId); //helper to contextually track active record in ui

    // Update all anchor hrefs with actual URLs based on job/quote type
    $contextMenu.find('a[data-url-template]').each(function() {
        const $link = $(this);
        const template = $link.data('url-template');
        let url;

        // Different logic for quotes vs jobs
        if ($link.hasClass('qa-quick-view')) {
            url = isJob ? 'Job/View/' + rId : 'Quote/View/' + rId;
        } else if ($link.hasClass('qa-edit-job')) {
            url = isJob ? 'Job/Edit/' + rId : 'Quote/Edit/' + rId;
        } else if ($link.hasClass('qa-time-tracking')) {
            if (isJob) {
                url = template.replace('{jobId}', rId).replace('{itemId}', '');
            } else {
                // Disable link for quotes
                $link.css('opacity', '0.5').css('pointer-events', 'none');
                return;
            }
            $link.css('opacity', '1').css('pointer-events', 'auto');
        } else if ($link.hasClass('qa-job-ticket')) {
            url = isJob ? 'GetJobTicket/' + rId : 'generate-customer-quote4/' + rId;
        } else if ($link.hasClass('qa-cost-sheet')) {
            if (isJob) {
                url = template.replace('{jobId}', rId);
            } else {
                // Disable link for quotes
                $link.css('opacity', '0.5').css('pointer-events', 'none');
                return;
            }
            $link.css('opacity', '1').css('pointer-events', 'auto');
        } else if ($link.hasClass('qa-duplicate-quote')) {
            if (!isJob) {
                url = 'Quote/Duplicate/' + rId;
            } else {
                // Disable link for jobs
                $link.css('opacity', '0.5').css('pointer-events', 'none');
                return;
            }
            $link.css('opacity', '1').css('pointer-events', 'auto');
        } else {
            url = template.replace('{jobId}', rId).replace('{itemId}', '');
        }

        $link.attr('href', ProcessUrl(url));
    });

    $contextMenu.css({
        top: offset.top + $jobElement.height(),
        left: (offset.left - 150)
    }).show();  // Position and show the context menu
};

function ResultActionsHandler(event) {
    showContextMenu(event, $(this));
};

function DuplicateQuoteHandler(event) {
    event.preventDefault();

    const quoteId = $('#qa_selected_job_id').val();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('quote/duplicate'),
        data: {
            id: quoteId
        },
        success: function (data) {
            // data is the new quote id - redirect to edit page
            window.location = ProcessUrl('quote/edit/' + data);
        },
        error: function () {
            alert('Failed to duplicate quote. Please try again.');
        }
    });
};

function DateFilterCheckbox() {
    let $cb = $(this);
    let checked = $cb.is(':checked');

    if (checked) {
        InitDatePicker();
    } else {
        DestroyDatePicker();
    }
};

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

    $('#created_dateref').prop('disabled', false);
    $('#completed_dateref').prop('disabled', false);
    $('#range_filter').prop('disabled', false);
};

function DestroyDatePicker() {
    $('#range_filter').daterangepicker("destroy");

    $('#created_dateref').prop('disabled', true);
    $('#completed_dateref').prop('disabled', true);
    $('#range_filter').prop('disabled', true);

    $('#range_filter').val('');
};

$(function () {
    $(document).on('click', function (event) {
        if (!$(event.target).closest('.result-actions').length && !$(event.target).closest('#job_context_menu').length) {
            $contextMenu.hide();
        }
    });

    //in case cache takes over, click all selected radio buttons to init their related behavior
    $('input[type="radio"]:checked').each(function () {
        $(this).trigger('click');
    });

    $('.add-search-term').trigger('click');
    $('#job_quote').focus();
})
    .on('click', '.submit-search-btn', SearchSubmit)
    .on('click', '.add-search-term', AddSearchTerm)
    .on('click', '.remove-me', RemoveSearchTerm)
    .on('click', 'input[name="clientSLRadio"]', ToggleClientFilter)
    .on('click', '#only_my_jobs', ToggleOnlyMyJobs)
    .on('click', '.reset-search', ResetSearchForm)
    .on('click', '.result-actions', ResultActionsHandler)
    .on('click', '#enable_df', DateFilterCheckbox)
    .on('click', '.qa-duplicate-quote', DuplicateQuoteHandler);