var irtsTable = $('#rts_table').dataTable({
    paging: false,
    columnDefs: [
        { targets: 0, visible: false } // Hide the first column (index 0)
    ]
});

function QAJobEdit() {
    //go to job edit
    let jobId = $('.job-context-menu').find('#qa_selected_job_id').val();
    window.location = ProcessUrl('Job/Edit') + '/' + jobId
};

function QATimeTracking() {
    //redirect to time tracking
    let jobId = $('.job-context-menu').find('#qa_selected_job_id').val();
    //note: need some sort of header to indicate that an auto-search should be performed
    window.location = ProcessUrl('time-tracking-hot-load') + '/' + jobId;
};

function QAJobTicket() {
    let jobId = $('.job-context-menu').find('#qa_selected_job_id').val();
    window.location = ProcessUrl('GetJobTicket') + '/' + jobId;
};

function ToggleJobDisplay() {
    if ($(this).is(':checked')) {
        let userId = getCookie('ActiveUser');

        $('tbody tr').hide();
        $('tbody tr[data-pm-id="' + userId + '"]').show();
    }
    else {
        $('tbody tr').show();
    }
};

function EnableShipmentBuild() {
    //show checkboxes next to each entry in the table
    irtsTable.DataTable().column(0).visible(true); // Show the first column
    //show the .complete-shipment button (disabled) and cancel-shipment button (always enabled)
    $('.ship-build-buttons').fadeIn('fast');
    //hide and disable the create shipment button
    $('.create-shipment-btn').prop('disabled', true);
    $('.create-shipment-btn').fadeOut('fast');
};

function CancelShipmentBuild() {
    //ensure submit button is disabled
    $('.to-details-btn').prop('disabled', true);
    //hide buttons
    $('.ship-build-buttons').fadeOut('fast');
    //trigger a click on all checked checkboxes to remove class
    $('table tbody tr input[type="checkbox"]:checked').each(function () {
        $(this).trigger('click');
    });
    //show and enable the create shipment button
    $('.create-shipment-btn').prop('disabled', false);
    $('.create-shipment-btn').fadeIn('fast');
    //hide the checkboxes in the table
    irtsTable.DataTable().column(0).visible(false);
};

function EnableDisableSubmitShipmentButton() {
    let ct = $('table tbody tr input[type="checkbox"]:checked').length;

    // Enable or disable the shipment button
    $('.to-details-btn').prop('disabled', ct === 0);

    // Efficiently update row classes based on checkbox state
    $('table tbody tr input[type="checkbox"]').each(function () {
        $(this).closest('tr').toggleClass('targeted-for-shipment', this.checked);
    });
};

function GetShipmentModel() {
    //return the title, notes and a list of selected job ids
    // Get Title and Notes values
    var title = $('#noteTitle').val().trim();
    var notes = $('#noteContent').val().trim();

    // Get the JobItemIds from the checked rows
    var jobItemIds = [];
    $('table tbody tr input[type="checkbox"]:checked').each(function () {
        var jobItemId = $(this).closest('tr').data('record-id');
        jobItemIds.push(parseInt(jobItemId)); // Convert to integer and add to the array
    });

    // Build the JSON object
    var jsonData = {
        Title: title,
        Notes: notes,
        JobItemIds: jobItemIds
    };

    return jsonData;
};

function SubmitShipment() {
    //build the shipment model
    //ajax to backend
    let model = GetShipmentModel();

    console.log(model);

    $.ajax({
        type: 'POST',
        url: ProcessUrl('add-shipment'),
        data: {
            shipment: model
        },
        success: function (data) {
            //alert that the record was saved
            alert('shipment saved!');
            //refresh the page
            window.location.reload();
        },
        error: function (err) {
            alert('unknown error saving shipment');
        }
    });
};

function ValidateShipmentDetails() {
    //just make sure the title has a value
    let titleLength = $('#notesModal #noteTitle').val().length;

    if (titleLength === 0) {
        alert('You must enter a title');
    } else {
        //submit the shipment
        SubmitShipment();
    }
};

function LoadItemInfoIntoModal() {
    var $list = $('#jobDetailsList');
    $list.empty(); // Clear previous data

    // Loop through checked checkboxes in the table
    $('table tbody tr input[type="checkbox"]:checked').each(function () {
        var $row = $(this).closest('tr');
        var jobNumber = $row.find('td:eq(1)').text().trim(); // Column 1 (Job Number)
        var client = $row.find('td:eq(3)').text().trim() || 'Unknown Client'; // Column 3 (Client Name)
        var description = $row.find('td:eq(4)').text().trim() || 'No description'; // Column 4 (Item Description)

        // Append the item to the list
        $list.append(`
            <li class="list-group-item">
                <strong>${jobNumber}</strong> - <em>${client}</em><br>
                ${description}
            </li>
        `);
    });
};

$(function () { })
    .on('click', '#user_toggle_checkbox', ToggleJobDisplay)
    .on('click', '.qa-edit-job', QAJobEdit)
    .on('click', '.qa-time-tracking', QATimeTracking)
    .on('click', '.qa-job-ticket', QAJobTicket)
    .on('click', '.create-shipment-btn', EnableShipmentBuild)
    .on('click', '.cancel-shipment-btn', CancelShipmentBuild)
    .on('change', 'table tbody tr input[type="checkbox"]', EnableDisableSubmitShipmentButton)
    .on('click', '.validate-details-btn', ValidateShipmentDetails)
    .on('show.bs.modal', '#notesModal', LoadItemInfoIntoModal);