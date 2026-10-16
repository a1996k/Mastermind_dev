const refreshInterval = 20000;
var autoRefresh;
var lastUpdate = '';

// Helper function to update element if the value has changed
function UpdateElementIfChanged($itemRow, selector, newValue, attribute) {
    let $element = $itemRow.find(selector);
    let currentValue = attribute ? $element.attr(attribute) : $element.html();

    if (currentValue !== newValue) {
        // Update value and add the 'auto-updated' class
        if (attribute) {
            $element.attr(attribute, newValue);
        } else {
            $element.html(newValue);
        }
        $element.addClass('auto-updated');
    }
};

function UpdateJobData(responseData) {
    for (let i = 0; i < responseData.length; i++) {
        let recordId = responseData[i].Id;
        let recSelector = `.job-item[data-record-id="${recordId}"]`;
        let $itemRow = $(recSelector);

        // Check if the value is different before updating
        UpdateElementIfChanged($itemRow, '.upd-job-number', responseData[i].JobNumber);
        UpdateElementIfChanged($itemRow, '.upd-job-description', responseData[i].TruncatedTitle, 'title');
        UpdateElementIfChanged($itemRow, '.upd-job-description', responseData[i].Description);
        UpdateElementIfChanged($itemRow, '.upd-min-start-date', responseData[i].StartBy);
        UpdateElementIfChanged($itemRow, '.upd-due-date', responseData[i].DueBy);
        UpdateElementIfChanged($itemRow, '.upd-days-remaining', responseData[i].DaysRemaining);
        UpdateElementIfChanged($itemRow, '.upd-job-client', responseData[i].Client);
    }
};

function checkServer() {
    $.ajax({
        type: 'POST',
        url: ProcessUrl('Refresh'),
        data: {
            LastUpdate: lastUpdate
        },
        success: function (data) {
            try {
                var res = JSON.parse(data);
                //lastUpdate = res.lastUpdate;
                if (res.shouldRefresh) {
                    // pick a random delay from 2000–6000 ms
                    const delay = Math.floor(Math.random() * 4000) + 2000;
                    setTimeout(() => {
                        location.reload();
                    }, delay);
                } else {
                    lastUpdate = res.lastUpdate;
                }

                //if res.records is not empty, this implies that the server has sent back updates
                //if (res.records.length > 0) {
                //    UpdateJobData(res.records);
                //}
            } catch (e) {
                console.error(e);
            }
        }
    });
};

function startAutoRefresh() {
    autoRefresh = setInterval(checkServer, refreshInterval);
};

function stopAutoRefresh() {
    clearInterval(autoRefresh);
};

$(function () {
    startAutoRefresh();
});