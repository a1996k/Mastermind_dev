let isReloadingCards = false; // Flag to prevent recursive reloads

function ReloadActiveCards() {
    if (isReloadingCards) {
        //console.log("ReloadActiveCards already in progress, skipping...");
        return;
    }

    //console.log("ReloadActiveCards called from:", new Error().stack);
    isReloadingCards = true;

    $('.ajaxable').each(function () {
        const daid = $(this).attr('data-ajax-id');
        //console.log(`Processing card ${daid}`);

        const $card = $(`.ajaxable[data-ajax-id="${daid}"]`);

        // Retrieve saved state
        const rangeStart = $card.data('rangeStart') || moment().subtract(7, 'days').format('YYYY-MM-DD');
        const rangeEnd = $card.data('rangeEnd') || moment().format('YYYY-MM-DD');
        const userSelections = $card.data('userSelections') || '-1'; // Default to all users

        /*//console.log(`Reloading card ${daid} with state:`, {
            rangeStart,
            rangeEnd,
            userSelections,
        });*/

        // Reload the card with the preserved state
        LoadDashboardCard(daid, rangeStart, rangeEnd, { userSelections });
    });

    //console.log("Initializing date pickers...");
    InitializeDateRangePickers();

    isReloadingCards = false;
};

function DashboardNav() {
    let $btn = $(this);
    $('.dashboard-button.db-selected').removeClass('db-selected');
    $btn.addClass('db-selected');

    let tabId = $btn.attr('data-record-id');
    $.ajax({
        type: 'POST',
        url: ProcessUrl('dashboard-cards-for-tab'),
        data: { tabId },
        success: function (data) {
            $('.dboard-ajax-tgt').html(data);
            ReloadActiveCards(); // Automatically initializes pickers
        },
        error: function () {
            alert('Error retrieving cards');
        }
    });
};

function applyCardState($card, cardState) {
    //console.log("Applying state to card:", cardState);

    // Restore date range picker state
    const $picker = $card.find('.date-range-picker');
    if ($picker.length && cardState.startDate && cardState.endDate) {
        const picker = $picker.data('daterangepicker');
        if (picker) {
            picker.setStartDate(cardState.startDate);
            picker.setEndDate(cardState.endDate);
        }
        $picker.val(`${cardState.startDate} - ${cardState.endDate}`);
        //console.log(`Date range applied: ${cardState.startDate} - ${cardState.endDate}`);
    }

    // Restore user selections
    const userSelections = (cardState.userSelections || '-1').split(',');
    const $modal = $(`#user-list-modal-${$card.data('ajax-id')}`);
    $modal.find('input[type="checkbox"]').each(function () {
        const $checkbox = $(this);
        const userId = $checkbox.closest('li').data('user-id').toString();
        const isChecked = userSelections.includes(userId) || userSelections.includes('-1');
        $checkbox.prop('checked', isChecked);
    });
    //console.log(`User selections applied: ${userSelections}`);

    // Restore other card states (isLocked, selectedRange)
    $card.find('input[name="Locked"]').prop('checked', cardState.isLocked);
    $card.find('select[name="Range"]').val(cardState.selectedRange);
};

let activeRequests = {}; // Object to track active requests by ajaxId

function LoadDashboardCard(ajaxId, rangeStart, rangeEnd, cardState = {}) {
    if (activeRequests[ajaxId]) {
        //console.log(`Card ${ajaxId} is already being loaded, skipping...`);
        return;
    }

    activeRequests[ajaxId] = true; // Lock this card

    const $card = $(`.ajaxable[data-ajax-id="${ajaxId}"]`);
    const userArrAsString = $card.data('userSelections') || cardState.userSelections || '-1';

    //console.log(`Sending request for card ${ajaxId} with users:`, userArrAsString);

    // Save the state to the card
    $card.data('rangeStart', rangeStart);
    $card.data('rangeEnd', rangeEnd);
    $card.data('userSelections', userArrAsString);

    const isLocked = $card.find('input[name="Locked"]').is(':checked');
    const selectedInterval = $card.find('select[name="Range"] option:selected').text() || 'Days';

    $.ajax({
        type: 'POST',
        url: ProcessUrl('get-dashboard-card'),
        data: {
            ajaxId,
            start: rangeStart,
            end: rangeEnd,
            users: userArrAsString,
            isLocked: isLocked || false,
            userRangeOverride: selectedInterval,
        },
        beforeSend: function () {
            $card.find('.loader').show();
        },
        success: function (data) {
            try {
                const response = JSON.parse(data);

                if (response.Html) {
                    $card.find('.card-data-wrapper').html(response.Html);
                    //console.log(`Card ${ajaxId} updated with new data.`);
                }

                // Reapply the preserved state
                InitializeDateRangePickers($card);

                // Apply user selections immediately to the modal
                applyUserSelectionsToModal(ajaxId, userArrAsString);

                applyCardState($card, {
                    startDate: rangeStart,
                    endDate: rangeEnd,
                    userSelections: userArrAsString,
                    isLocked,
                    selectedRange: selectedInterval,
                });

                if (response.Records) {
                    //console.log('Here is response data');
                    //console.log(response);
                    Charts(ajaxId, response.Records, response.UserTotals, response.ChartMeta.ChartType, response.DatasetAverages, response.TargetUtilization);
                }
            } catch (e) {
                //console.error(`Error parsing response for card ${ajaxId}:`, e);
            }
        },
        complete: function () {
            $card.find('.loader').hide();
            delete activeRequests[ajaxId]; // Release lock
        },
        error: function (xhr) {
            //console.error(`Failed to load card ${ajaxId}:`, xhr.responseText);
            delete activeRequests[ajaxId];
        },
    });
}

/**
 * Applies the user selections to the modal checkboxes.
 */
function applyUserSelectionsToModal(ajaxId, userArrAsString) {
    const userSelections = userArrAsString.split(',');
    const $modal = $(`#user-list-modal-${ajaxId}`);

    //console.log(`Applying user selections to modal for card ${ajaxId}:`, userSelections);

    $modal.find('input[type="checkbox"]').each(function () {
        const $checkbox = $(this);
        const userId = $checkbox.closest('li').data('user-id').toString();
        $checkbox.prop('checked', userSelections.includes(userId) || userSelections.includes('-1'));
    });

    //console.log(`User selections applied to modal for card ${ajaxId}`);
}

function LoadState() {
    const userId = getCookie('ActiveUser');
    if (!userId) return;

    const state = JSON.parse(localStorage.getItem(`dashboardState_${userId}`)) || {
        config: {
            dateRange: '',
            tab: '',
            charts: []
        }
    };

    let tabName = state.config.tab;

    $('.db-selected').removeClass('db-selected');
    $(`.dashboard-button[value="${tabName}"]`).addClass('db-selected');

    $('#range_filter').val(state.config.dateRange);
    $('#range_filter').trigger('change');

    // Apply saved state to cards
    state.config.charts.forEach(chartState => {
        const $card = $(`.ajaxable[data-ajax-id="${chartState.ajaxId}"]`);
        applyCardState($card, chartState);
    });

    //console.log("Dashboard state loaded:", state);
}

function SaveState() {
    const userId = getCookie('ActiveUser');
    if (!userId) return;

    const state = {
        config: {
            dateRange: $('#range_filter').val(),
            tab: document.querySelector('.db-selected').value,
            charts: []
        }
    };

    document.querySelectorAll('.card-data-wrapper').forEach(chart => {
        const chartParent = $(chart).parents('.ajaxable')[0];
        const ajaxId = $(chartParent).attr('data-ajax-id');
        const $modal = $(`#user-list-modal-${ajaxId}`);
        const selectedUsers = $modal.find('input[type="checkbox"]:checked')
            .map(function () {
                return $(this).closest('li').data('user-id');
            })
            .get()
            .join(',');

        state.config.charts.push({
            ajaxId,
            rangeOverride: $(chartParent).find('select[name="Range"]').val(),
            rangeLocked: $(chartParent).find('input[name="Locked"]').is(':checked'),
            userSelections: selectedUsers
        });
    });

    localStorage.setItem(`dashboardState_${userId}`, JSON.stringify(state));
    //console.log("Dashboard state saved:", state);
}

function RefreshDashboard() {
    ReloadActiveCards();
};

function RangeSelection() {
    $(this).parent().find('input[type="checkbox"]').prop('checked', true);
};

function ShowHideChartDescription() {
    let $btn = $(this);
    let $info = $btn.parents('.card-data-wrapper').find('.informational-block');

    if ($info.is(':visible')) {
        $info.fadeOut('fast');
    } else {
        $info.fadeIn('fast');
    }
};

function getUserSelections($card) {
    const userSelections = [];
    $card.find('.Users-msl-dropdown-container ul li input[type="checkbox"]:checked').each(function () {
        userSelections.push($(this).attr('value'));
    });
    return userSelections;
};

function ReloadCardWithDateRange($card, startDate, endDate) {
    const ajaxId = $card.data('ajax-id');
    //console.log(`Reloading card ${ajaxId} with Start=${startDate}, End=${endDate}`);

    // Determine user list
    const userArr = [];
    const $userCheckboxes = $card.find('.Users-msl-dropdown-container ul li input[type="checkbox"]');
    const checkedUserCount = $userCheckboxes.filter(':checked').length;
    const totalUserCount = $userCheckboxes.length;

    // If all users are selected, send -1; otherwise, collect selected user IDs
    const userArrAsString = (checkedUserCount === totalUserCount)
        ? '-1'
        : $userCheckboxes.filter(':checked').map(function () {
            return $(this).val();
        }).get().join(',');

    //console.log(`User list for card ${ajaxId}:`, userArrAsString);

    // Capture current state of the card
    const cardState = {
        startDate,
        endDate,
        userSelections: userArrAsString,
        isLocked: $card.find('input[name="Locked"]').is(':checked'),
        selectedRange: $card.find('select[name="Range"]').val(),
    };

    // Pass the state along with the reload request
    LoadDashboardCard(ajaxId, startDate, endDate, cardState);
}

function InitializeDateRangePickers($cards = $('.ajaxable')) {
    //console.log("Initializing date range pickers...");

    $cards.each(function () {
        const $card = $(this);
        const $picker = $card.find('.date-range-picker');

        if ($picker.length) {
            //console.log(`Initializing picker for ${$picker.attr('id')}`);

            const rangeStart = $card.data('rangeStart') || moment().subtract(7, 'days').format('YYYY-MM-DD');
            const rangeEnd = $card.data('rangeEnd') || moment().format('YYYY-MM-DD');

            //console.log(`Picker initialization for ${$picker.attr('id')} with range: ${rangeStart} - ${rangeEnd}`);

            // Destroy existing picker instance to prevent duplicates
            if ($picker.data('daterangepicker')) {
                $picker.data('daterangepicker').remove();
            }

            // Initialize the date picker with preserved state
            $picker.daterangepicker({
                autoUpdateInput: true,
                locale: {
                    format: 'YYYY-MM-DD',
                    cancelLabel: 'Clear',
                },
                startDate: rangeStart,
                endDate: rangeEnd,
                ranges: {
                    'Today': [moment(), moment()],
                    'Yesterday': [moment().subtract(1, 'days'), moment().subtract(1, 'days')],
                    'Last 7 Days': [moment().subtract(6, 'days'), moment()],
                    'Last 30 Days': [moment().subtract(29, 'days'), moment()],
                    'This Month': [moment().startOf('month'), moment().endOf('month')],
                    'Last Month': [moment().subtract(1, 'month').startOf('month'), moment().subtract(1, 'month').endOf('month')],
                },
            }, function (start, end) {
                //console.log(`Date range updated for card: ${$card.data('ajax-id')}`);
                $card.data('rangeStart', start.format('YYYY-MM-DD'));
                $card.data('rangeEnd', end.format('YYYY-MM-DD'));
                ReloadCardWithDateRange($card, start.format('YYYY-MM-DD'), end.format('YYYY-MM-DD'));
            });

            const picker = $picker.data('daterangepicker');
            $picker.val(`${picker.startDate.format('YYYY-MM-DD')} - ${picker.endDate.format('YYYY-MM-DD')}`);
        }
    });
};

function CardUserSelectAllHandler() {
    let $ele = $(this);
    let checked = $ele.is(':checked');

    //console.log("Clicked checkbox:", $ele);
    //console.log("Checked state:", checked);

    // Update all checkboxes in the same UL
    $ele.closest('ul').find('li input[type="checkbox"]').prop('checked', checked);
};

$(function () {
    rangeStart = moment().subtract(1, 'days').format("YYYY-MM-DD");
    rangeEnd = moment().subtract(1, 'days').format("YYYY-MM-DD");

    $(document).ajaxStop(function () {
        $('.refresh-dashboard').removeClass('spin');
        $('.refresh-dashboard').on('click', RefreshDashboard);
        $('#range_filter').prop('disabled', false);
    });

    $(document).ajaxStart(function () {
        $('#range_filter').prop('disabled', true);
        $('.refresh-dashboard').off('click');
        $('.refresh-dashboard').addClass('spin');
    });

    $(document).on('click', '.user-list-submitted', function () {
        const $modal = $(this).closest('.modal');
        const ajaxId = $modal.attr('id').split('-').pop();
        const $card = $(`.ajaxable[data-ajax-id="${ajaxId}"]`);

        // Gather selected users
        const selectedUsers = $modal.find('input[type="checkbox"]:checked')
            .map(function () {
                return $(this).closest('li').data('user-id');
            })
            .get()
            .join(',') || '-1';

        // Save updated user selections to the card
        $card.data('userSelections', selectedUsers);

        //console.log(`Updated userSelections for card ${ajaxId}: ${selectedUsers}`);

        // Reload the card
        ReloadActiveCards();
    });

    $('.metric-wrapper').sortable();

    let $initTab = $('.tabs input:first-child');
    $initTab.trigger('click');
})
    .on('click', '.dashboard-button', DashboardNav)
    .on('change', 'select[name="Range"]', RangeSelection)
    .on('click', 'ul[class$="-user-list"] li[data-user-id="-1"]', CardUserSelectAllHandler)
    /*.on('click', 'input[name="Locked"], .dashboard-button', SaveState)*/
    .on('click', '.card-data-wrapper i.fa-circle-info', ShowHideChartDescription);