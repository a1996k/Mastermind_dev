var printTable;

function JobItemSelect(e) {
    var $j = $(this);
    let rId = $j.attr('data-record-id');
    let iId = $j.attr('data-item-id');
    $('.job-context-menu #qa_selected_job_id').val(rId);
    $('.job-context-menu #qa_selected_item_id').val(iId);
    //toggle the clicked job class on the clicked element
    $j.toggleClass('clicked-job');
    //remove clicked-job from .job-item elements that are not $j
    $('.job-item').not($j).removeClass('clicked-job');
    //if .toast-overlay has auto-slide-hidden and the amount of .clicked-job elements is not zero, click the toast-overlay
    //if .toast-overlay has auto-slide-shown and the amount of .clicked-job elements is zero, click the toast-overlay
    if (
        ($('.toast-overlay').hasClass('auto-slide-hidden') && $('.clicked-job').length > 0) ||
        ($('.toast-overlay').hasClass('auto-slide-shown') && $('.clicked-job').length == 0)
    ) {
        $('.toast-overlay').trigger('click');
    }
};

function EmptyJobListHandler() {
    $('ul.empty-list').removeClass('empty-list');
    $('ul:not(:has(li))').addClass('empty-list');
};

function ToggleQuickEditButtonDisplay() {
    $('.quick-edit-buttons').toggleClass('d-none');
};

function DestroyJobTable() {
    printTable.DataTable().destroy(false);
};

$(function () {
    //InstantiateJobTable();

    EmptyJobListHandler();

    // Function to update the clock every second
    function updateClock() {
        const clockElement = document.getElementById('clock');
        const now = new Date();

        let hours = now.getHours();
        let minutes = now.getMinutes();
        let seconds = now.getSeconds();
        const ampm = hours >= 12 ? 'PM' : 'AM';

        hours = hours % 12;
        hours = hours ? hours : 12; // 0 should be displayed as 12
        hours = hours < 10 ? '0' + hours : hours;
        minutes = minutes < 10 ? '0' + minutes : minutes;
        seconds = seconds < 10 ? '0' + seconds : seconds;

        clockElement.innerHTML = `${hours}:${minutes}:${seconds} ${ampm}`;
    }
    if ($('#clock').length) {
        // Update the clock every second
        setInterval(updateClock, 1000);

        // Initialize the clock immediately when the page loads
        updateClock();
    }
})
    .on('click', '.job-item', JobItemSelect);