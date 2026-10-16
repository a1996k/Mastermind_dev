$(function () {
    let userId = getCookie('ActiveUser');
    $('.my-bugs-filter').attr('value', userId);
    let dTable = $('#bug_table').DataTable();

    // Function to filter based on the current user ID
    function filterMyBugs() {
        var userId = $('.my-bugs-filter').val(); // e.g., "123"
        if ($('.my-bugs-filter').is(':checked')) {
            // Add the filter if it's not already added
            $.fn.dataTable.ext.search.push(myBugsFilterFunction);
        } else {
            // Remove the filter when the checkbox is unchecked
            $.fn.dataTable.ext.search = $.fn.dataTable.ext.search.filter(function (filter) {
                return filter !== myBugsFilterFunction;
            });
        }
        dTable.draw(); // Redraw the table
    }

    // The filter function for "My Bugs"
    function myBugsFilterFunction(settings, data, dataIndex) {
        var userId = $('.my-bugs-filter').val(); // Get current user ID
        var userIdAttr = $('td:eq(1)', dTable.row(dataIndex).node()).data('user-id');
        console.log(userIdAttr);
        return userIdAttr == userId;
    }

    // Function to filter based on whether the bug is completed (FixDate has a value)
    function filterCompletedBugs() {
        if ($('.bug-complete-filter').is(':checked')) {
            // Add the filter if it's not already added
            $.fn.dataTable.ext.search.push(completedBugsFilterFunction);
        } else {
            // Remove the filter when the checkbox is unchecked
            $.fn.dataTable.ext.search = $.fn.dataTable.ext.search.filter(function (filter) {
                return filter !== completedBugsFilterFunction;
            });
        }
        dTable.draw(); // Redraw the table
    }

    // The filter function for completed bugs
    function completedBugsFilterFunction(settings, data, dataIndex) {
        var fixDate = $('td:eq(8)', dTable.row(dataIndex).node()).text(); // FixDate is in column 9 (0-indexed)
        console.log(fixDate);
        return fixDate.trim().length === 0; // If FixDate has a value, bug is completed
    }

    // Event listener for the checkboxes
    $('.filter').on('change', function () {
        filterMyBugs();       // Apply or remove the "my bugs" filter
        filterCompletedBugs(); // Apply or remove the "completed bugs" filter
    });
});