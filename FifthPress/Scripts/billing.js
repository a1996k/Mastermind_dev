/* NOTE: Code was changed 12/4/2024 on the fly to accomodate Kelly needing a place to see ready to bill jobs/set as billed */

function OpenInvoiceInNewTab() {
    let $row = $(this).parents('tr');
    let jId = $row.attr('data-record-id');

    window.open(ProcessUrl('job-invoice') + '/' + jId);
};

function MarkJobAsBilled() {
    let $row = $(this).parents('tr');
    let jId = $row.attr('data-record-id');

    //ajax to mark as billed
    $.ajax({
        type: 'POST',
        url: ProcessUrl('ajax-mark-as-billed'),
        data: {
            jobId: jId
        },
        success: function () {
            alert('billing status updated');
            $row.remove();
        }
    });
};

function GoToBillingReports() {
    window.location = ProcessUrl('billing/reports');
};

$(function () {
    $('#ready_to_bill_table').DataTable({
        "columnDefs": [
            {
                "targets": [4, 5, 6, 7],
                "render": function (data, type, row) {
                    // Adjust the date format to match the format in your table
                    var date = moment(data, "M/D/YYYY", true);

                    if (type === 'display' || type === 'filter') {
                        if (!date.isValid()) {
                            return ''; // Custom message for invalid date
                        }
                        return date.format('M/D/YYYY'); // Display format
                    }

                    // Use ISO format for sorting
                    return date.isValid() ? date.toISOString() : ''; // ISO format ensures proper sorting
                }
            }
        ]
    });
})
    .on('click', '.view-invoice', OpenInvoiceInNewTab)
    .on('click', '.mark-as-billed', MarkJobAsBilled)
    .on('click', '.go-to-reports', GoToBillingReports);