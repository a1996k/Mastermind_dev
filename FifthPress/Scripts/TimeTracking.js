let $activeBtnForServiceCheck;
let $activeCompleteButton;
let qaRecordModel;

//Timer helper functions
//currently, this function is used to assist with updating the elapsed milliseconds on the live page
function parseTimeToMilliseconds(timeStr) {
    let [hours, minutes, seconds] = timeStr.split(':').map(Number);
    return (hours * 3600000) + (minutes * 60000) + (seconds * 1000);
};

function stopTimer(serviceId) {
    let timerId = 'mt_' + serviceId;
    const timerIndex = individualTimers.findIndex(t => t.id === timerId);

    const timer = individualTimers.find(t => t.id === timerId);
    if (timer) {
        // Stop the timer if it has a method or interval you need to clear
        clearInterval(individualTimers[timerIndex].timerID);

        // Remove the timer from the array
        individualTimers.splice(timerIndex, 1);
    } else {
        console.error(`Timer with id ${timerId} not found`);
    }
};

function updateTimeTrackingTimers() {
    const now = new Date();

    individualTimers.forEach(timer => {
        // Skip the timer update if it has an endTime (meaning it's stopped)
        if (timer.endTime) {
            return;
        }

        // Check if startTime is already a Date object, if not, convert it
        if (!(timer.startTime instanceof Date)) {
            timer.startTime = new Date(timer.startTime);
        }

        // Validate the startTime, skip if it's not a valid date
        if (isNaN(timer.startTime.getTime())) {
            console.error(`Invalid startTime for timer id ${timer.id}`);
            return;
        }

        // Calculate the elapsed time in milliseconds
        const elapsedMs = now - timer.startTime;

        // Calculate hours, minutes, and seconds from elapsed time
        const hours = Math.floor((elapsedMs / (1000 * 60 * 60)) % 24);
        const minutes = Math.floor((elapsedMs / (1000 * 60)) % 60);
        const seconds = Math.floor((elapsedMs / 1000) % 60);

        // Format the time as HH:MM:SS for display
        const formattedTime = String(hours).padStart(2, '0') + ":" +
            String(minutes).padStart(2, '0') + ":" +
            String(seconds).padStart(2, '0');

        // Update the timer display in the DOM
        const timerElement = document.getElementById(`${timer.id}`);
        if (timerElement) {
            timerElement.textContent = formattedTime;
        } else {
            console.warn(`No element found for timer id ${timer.id}`);
        }
    });
};

function updateElapsedMs(sId) {
    let $entry = $('.service-list-row .service-entry[data-record-id="' + sId + '"]');
    let timeDisplayId = '#mt_' + sId;
    let curDisplay = ($entry.find(timeDisplayId)).html().trim();
    let ms = parseTimeToMilliseconds(curDisplay);

    $entry.find('input.elapsed-ms').val(ms);
};

function StartServiceCheck() {
    var $btn = $(this);
    $btn.prop('disabled', true);
    $activeBtnForServiceCheck = $btn;

    $.ajax({
        type: 'GET',
        url: ProcessUrl('start-service-check'),
        success: function (data) {
            //if showPrompt === true
            if (data.showPrompt === true) {
                let $tgt = $('#duplicate_service_modal .active-service-wrapper');
                $tgt.html('');

                for (let i = 0; i < data.services.length; i++) {
                    $tgt.append(
                        `
                            <div class="active-service fs-14 d-flex flex-row justify-content-between mt-3" data-jis-id="${data.services[i].JobItemServiceId}" data-record-id="${data.services[i].Id}">
                                <div class="col-4 field-binder-row-flex">
                                    Job #${data.services[i].JobNumber}.${data.services[i].Ordinal}
                                </div>
                                <div class="col-8 field-binder-row-flex justify-content-end">
                                    <b>${data.services[i].Title}</b> ${data.services[i].StartTime}
                                    <button type="button" class="btn btn-sm btn-fc-danger end-service-btn ms-5">Stop</button>
                                <div>
                            </div>
                            `
                    );
                }

                $('#duplicate_service_modal #ds_prompt_cancel_helper').val($btn.attr('data-record-id'));

                $('#duplicate_service_modal').modal('show');
            } else {
                StartService();
            }
        },
        complete: function () {
            $btn.prop('disabled', false);
        }
    });
};

//EndService is multi service modal specific
function EndService() {
    let $btn = $(this);
    let serviceId = $btn.parents('.active-service').attr('data-record-id');
    let jisId = $btn.parents('.active-service').attr('data-jis-id');

    //remove the row from the duplicate services modal
    $btn.parents('.active-service').remove();

    //$('.stop-service[data-record-id="' + jisId + '"]').trigger('click');
    $('.service-list-row .service-entry[data-record-id="' + jisId + '"]').find('.stop-task-btn').trigger('click');
};

function removeSideCard(sId) {
    //try to find a side menu card
    let selector = '.sm-tt-wrapper .timer-display#td_' + sId;

    $(selector).parents('.sm-tt-wrapper').remove();
};

function StartGeneralTask() {
    let $btn = $(this);
    let serviceId = $btn.parents('.gen-service-entry').attr('data-record-id');
    let eMs = $btn.parents('.gen-service-entry').find('.elapsed-ms').val() || 0; // Default to 0 if not found

    // Ensure eMs is an integer
    eMs = parseInt(eMs, 10);

    $.ajax({
        type: 'POST',
        url: ProcessUrl('tt-start-task'),
        data: {
            serviceId
        },
        success: function (data) {
            try {
                let res = JSON.parse(data);

                if (res.IsGeneralTask) {
                    $('#qa_check_modal').modal('show');
                }
            } catch (e) { console.log(e); }
            // Start the timer
            addTimeTrackingTimer(serviceId, eMs);

            $btn.hide();
            $btn.parent().find('.stop-general-task-btn').show();
        },
        error: function (err) {
            console.log(err);
        }
    });
};

function StopGeneralTask() {
    let $btn = $(this);
    let serviceId = $btn.parents('.gen-service-entry').attr('data-record-id');

    $.ajax({
        type: 'POST',
        url: ProcessUrl('tt-stop-task'),
        data: {
            serviceId
        },
        success: function (data) {
            //stop the timer
            stopTimer(serviceId);
            removeSideCard(serviceId);
            updateElapsedMs(serviceId);
            $btn.hide();
            $btn.parent().find('.start-general-task-btn').show();
        },
        error: function (err) {
            console.log(err);
        }
    });
};

function StartService() {
    let $btn = $activeBtnForServiceCheck;
    let sId = $btn.parents('.service-entry').attr('data-record-id');
    let eMs = $btn.parents('.service-entry').find('.elapsed-ms').val();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('tt-start-task'),
        data: {
            serviceId: sId
        },
        success: function (data) {
            try {
                let res = JSON.parse(data);

                if (res.NeedsQACheck) {
                    $('#qa_check_modal').data('is-print-qa', res.IsPrintQA);
                    $('#qa_check_modal').modal('show');
                }
            } catch (e) { console.log(e); }
            //start the timer
            addTimeTrackingTimer(sId, eMs);

            $btn.hide();
            $btn.parent().find('.stop-task-btn').show();
        },
        error: function (err) {
            console.log(err);
        }
    });
};
//this one is called when the "Stop" button is pressed on the main index page
function StopTask() {
    let $btn = $(this);
    let sId = $btn.parents('.service-entry').attr('data-record-id');

    $.ajax({
        type: 'POST',
        url: ProcessUrl('tt-stop-task'),
        data: {
            serviceId: sId
        },
        success: function (data) {
            //stop the timer
            stopTimer(sId);
            removeSideCard(sId);
            updateElapsedMs(sId);
            $btn.hide();
            $btn.parent().find('.start-task-btn').show();
        },
        error: function (err) {
            console.log(err);
        }
    });
};
function CompletePromptCheck() {
    let $cb = $(this);
    $activeCompleteButton = $cb;
    let sId = $cb.parents('.service-entry').attr('data-record-id');
    let checked = $cb.is(':checked');

    $.ajax({
        type: 'POST',
        url: ProcessUrl('tt-complete-check'),
        data: {
            id: sId,
            complete: checked
        },
        success: function (data) {
            if (data !== '') {
            } else {
                console.log('No Prompts');
            }

            CompleteToggle($cb);
        },
        error: function (err) {
            alert(err);
        }
    });
};
function CompleteToggle($cbOverride) {
    let $cb = $(this);
    if ($cbOverride) {
        $cb = $cbOverride;
    }
    let sId = $cb.parents('.service-entry').attr('data-record-id');
    let checked = $cb.is(':checked');

    $.ajax({
        type: 'POST',
        url: ProcessUrl('tt-complete-toggle'),
        data: {
            id: sId,
            complete: checked
        },
        success: function () {
            if (checked) {
                console.log('Service Completed');
                //if checked, close the active service for this user
                let $stopBtn = $cb.parents('.service-entry').find('.stop-task-btn');

                if ($stopBtn.is(':visible')) {
                    setTimeout(function () {
                        $stopBtn.trigger('click');
                    }, 250);
                }
            } else {
                console.log('Service Uncompleted');
            }
        },
        error: function (data) {
            console.log(data);
        }
    });
};

function GoToItem() {
    let $clicked = $(this);
    let jId = $clicked.parents('.service-entry').attr('data-job-id');
    let jiId = $clicked.parents('.service-entry').attr('data-item-id');

    window.location = ProcessUrl('time-tracking') + '/' + jId + '/' + jiId;
};

function OpenQAModal() {
    let jisId = $(this).attr('data-jis-id');  // Make sure this is the right selector

    var jisIdInt = parseInt(jisId, 10); // Convert to integer if necessary
    var relatedQAs = qaRecordJson.filter(r => r.JobItemService === jisIdInt);

    var qaHtml = "";
    relatedQAs.forEach(function (record) {
        // Split responses into two blocks
        let halfIndex = Math.ceil(record.Responses.length / 2);
        let firstResponses = record.Responses.slice(0, halfIndex);
        let secondResponses = record.Responses.slice(halfIndex);

        // Generate HTML for each block
        let firstQABlock = generateQABlock(firstResponses);
        let secondQABlock = generateQABlock(secondResponses);

        let resultText = record.Passed ? 'Pass' : 'Fail';

        qaHtml +=
            `
        <tr>
            <td> ${resultText} </td>
            <td> ${record.JobNumber}.${record.ItemNumber}</td>
            <td> ${record.ServiceTitle} </td>
            <td> ${record.FirstApprovalUser} </td>
            <td> ${record.FirstApprovalTimeString} </td>
            <td> ${firstQABlock} </td>
            <td> ${record.SecondApprovalUser} </td>
            <td> ${record.SecondApprovalTimeString} </td>
            <td> ${secondQABlock} </td>
        </tr>
        `;
    });

    $('#qaRecordsContainer').html(qaHtml); // Insert the rows into the tbody
    $('#qa_list_modal').modal('show'); // Show the modal
};

function generateQABlock(responses) {
    let blockHtml = '<ul>';
    responses.forEach(response => {
        let affirmativeText = response.Response ? 'Yes' : 'No'; // Determine the response text
        let notes = response.Notes ? ` - ${response.Notes}` : ''; // Add notes if they exist
        blockHtml += `<li>${response.Question}: ${affirmativeText}${notes}</li>`;
    });
    blockHtml += '</ul>';
    return blockHtml;
};

function PreCompletePromptCancel() {
    $activeCompleteButton.prop('checked', false);
};

function PullMaterialInputsAreValid() {
    $('#pull_material_modal .pull-material-row').each(function () {
        let $row = $(this);
        let $inp = $row.find('input');

        if ($inp.val() === '') {
            return false;
        }
    });

    return true;
};

function GetPulledMaterialsModel() {
    let model = [];

    $('#pull_material_modal .pull-material-row').each(function () {
        let $row = $(this);

        let materialPull = {
            MaterialId: $row.attr('data-material-id'),
            AmountToPull: $row.find('.atp-display').html().trim(),
            AmountPulled: $row.find('input').val(),
            MaterialCode: "",
            MaterialNotes: "",
            FriendlyName: "",
            UnitText: ""
        };

        if (materialPull.AmountPulled > 0) {
            model.push(materialPull);
        }
    });

    return model;
};

function GetPulledInksModel() {
    let model = [];

    $('#pull_ink_modal .ink-item').each(function () {
        let $row = $(this);

        let inkPull = {
            MaterialId: $row.attr('data-material-id'),
            AmountToPull: 0,
            AmountPulled: $row.find('input').val(),
            MaterialCode: "",
            MaterialNotes: "",
            FriendlyName: "",
            UnitText: ""
        };

        if (inkPull.AmountPulled > 0) {
            model.push(inkPull);
        }
    });

    return model;
};

function CompletePullInk() {
    let pulledInks = GetPulledInksModel();

    if (pulledInks.length > 0) {
        let jobId = $('#Job_Id').val();
        let jobItemId = $('#JobItem_Id').val();

        $.ajax({
            type: 'POST',
            url: ProcessUrl('pull-material-complete'),
            data: {
                pulls: pulledInks,
                jobId,
                jobItemId
            },
            error: function () {
                alert('error pulling inks');
            }
        });
    }
};

function CompletePullMaterial() {
    //validate fields for entry
    if (PullMaterialInputsAreValid()) {
        let pulledMaterials = GetPulledMaterialsModel();

        //only submit if materials were actually pulled
        if (pulledMaterials.length > 0) {
            let jobId = $('#Job_Id').val();
            let jobItemId = $('#JobItem_Id').val();

            $.ajax({
                type: 'POST',
                url: ProcessUrl('pull-material-complete'),
                data: {
                    pulls: pulledMaterials,
                    jobId,
                    jobItemId
                },
                success: function (data) {
                    // Check if all rows in the modal are now fully pulled
                    let allFullyPulled = true;
                    $('#pull_material_modal .pull-material-row').each(function () {
                        let pulled = parseInt($(this).find('input').val()) || 0;
                        let remaining = parseInt($(this).find('.atp-display').text().trim()) || 0;
                        if (pulled < remaining) allFullyPulled = false;
                    });
                    // Remove any existing indicator
                    $('.material-pull-check').remove();
                    if (allFullyPulled) {
                        $('.open-pm-modal').after('<i class="fa-solid fa-circle-check ms-3 fs-18 material-pull-check" style="color: #28a745;" title="Fully Pulled"></i>');
                        // Auto-check the Complete checkbox on the Pull Material service row
                        let $pmEntry = $('.open-pm-modal').closest('.service-entry[data-record-id]');
                        let $pmCb = $pmEntry.find('.service-complete-cb');
                        if ($pmCb.length && !$pmCb.is(':checked')) {
                            $pmCb.prop('checked', true);
                            CompleteToggle($pmCb);
                        }
                    } else {
                        $('.open-pm-modal').after('<i class="fa-solid fa-circle-half-stroke ms-3 fs-18 material-pull-check" style="color: #f0ad4e;" title="Partially Pulled"></i>');
                    }
                },
                error: function (err) {
                    alert('Error pulling materials');
                }
            });
        }
    }
};

function LoadMaterialPullModal() {
    let jobItem = $('#JobItem_Id').val();

    $.ajax({
        type: 'GET',
        url: ProcessUrl('open-pull-material-modal') + '/' + jobItem,
        success: function (data) {

            // Load the pulls section
            $('#pull_material_modal .pulls').html('');

            for (let i = 0; i < data.length; i++) {
                let html =
                    `
                    <div class="pull-material-row mt-3" data-material-id="${data[i].MaterialId}">
                        <span class="ff-poppins fs-16 fw-bold d-block">${data[i].MaterialTitle}(${data[i].FriendlyName})</span>
                        <div class="field-binder-row-flex">
                            <input type="number" class="_val_-gt-zero" value="0" min="0" /> <span class="ff-poppins fs-16 fw-bold ms-1"> / </span><span class="ff-lato fs-16 fw-bold ms-1 atp-display">${data[i].AmountToPull}</span>
                        </div>
                    </div>
                    `;

                $('#pull_material_modal .pulls').append(html);
            }

            // Load the history section
            $('#pull_material_modal .history').html('');

            for (let i = 0; i < data.length; i++) {
                if (data[i].History && data[i].History.length > 0) {
                    let materialTitle = data[i].MaterialTitle || "Unknown Material";

                    let html =
                        `
                        <ul class="pull-history-row mt-3">
                            <span class="ff-poppins fs-16 fw-bold d-block">History for ${materialTitle}</span>
                            <div class="field-binder-col-flex">
                                ${data[i].History.map(record => `
                                <li>
                                    <span class="ff-dm-sans fs-14">
                                        <span class="fw-bold">${record.PulledBy}</span> pulled
                                        <span class="fw-bold">${record.PulledQuantity} ${(record.QuantityUnit === 'Feet' ? 'Inches' : record.QuantityUnit)}</span> on
                                        <span class="fw-bold">${record.PulledDate}</span>
                                    </span>
                                </li>
                                `).join('')}
                            </div>
                        </ul>
                        `;

                    $('#pull_material_modal .history').append(html);
                }
            }

            $('#pull_material_modal .pulls').append(`<span class="fst-italic fs-16">When pulling roll stock, your entry will be increased to the nearest multiple of <b>12</b></span>`);
        },
        error: function (err) {
            alert(err);
        }
    });
};

function CollapseUIHandler() {
    let $ele = $(this);
    let $caret = $ele.find('.fa-caret-up');
    $caret.toggleClass('rotated');
};

function LoadFinishingMaterialPullModal() {
    let jifmId = $('#pull_finishing_material_modal').data('jifm-id');

    $.ajax({
        type: 'GET',
        url: ProcessUrl('open-pull-finishing-material-modal') + '/' + jifmId,
        success: function (data) {
            $('#pull_finishing_material_modal .pulls').html('');
            $('#pull_finishing_material_modal .history').html('');

            for (let i = 0; i < data.length; i++) {
                let html =
                    `<div class="pull-material-row mt-3" data-material-id="${data[i].MaterialId}">
                        <span class="ff-poppins fs-16 fw-bold d-block">${data[i].MaterialTitle} (${data[i].FriendlyName})</span>
                        <div class="field-binder-row-flex">
                            <input type="number" class="_val_-gt-zero" value="0" min="0" /> <span class="ff-poppins fs-16 fw-bold ms-1"> / </span><span class="ff-lato fs-16 fw-bold ms-1 atp-display">${data[i].AmountToPull}</span>
                            <span class="ff-lato fs-14 ms-2">${data[i].UnitText}</span>
                        </div>
                    </div>`;
                $('#pull_finishing_material_modal .pulls').append(html);
            }

            for (let i = 0; i < data.length; i++) {
                if (data[i].History && data[i].History.length > 0) {
                    let html =
                        `<ul class="pull-history-row mt-3">
                            <span class="ff-poppins fs-16 fw-bold d-block">History for ${data[i].MaterialTitle}</span>
                            <div class="field-binder-col-flex">
                                ${data[i].History.map(record => `
                                <li>
                                    <span class="ff-dm-sans fs-14">
                                        <span class="fw-bold">${record.PulledBy}</span> pulled
                                        <span class="fw-bold">${record.PulledQuantity} ${record.QuantityUnit}</span> on
                                        <span class="fw-bold">${record.PulledDate}</span>
                                    </span>
                                </li>
                                `).join('')}
                            </div>
                        </ul>`;
                    $('#pull_finishing_material_modal .history').append(html);
                }
            }
        },
        error: function (err) {
            alert('Error loading finishing material pull info');
        }
    });
};

function CompletePullFinishingMaterial() {
    let model = [];
    $('#pull_finishing_material_modal .pull-material-row').each(function () {
        let $row = $(this);
        let materialPull = {
            MaterialId: $row.attr('data-material-id'),
            AmountToPull: $row.find('.atp-display').html().trim(),
            AmountPulled: $row.find('input').val(),
            MaterialCode: "",
            MaterialNotes: "",
            FriendlyName: "",
            UnitText: ""
        };
        if (materialPull.AmountPulled > 0) {
            model.push(materialPull);
        }
    });

    if (model.length > 0) {
        let jobId = $('#Job_Id').val();
        let jobItemId = $('#JobItem_Id').val();

        $.ajax({
            type: 'POST',
            url: ProcessUrl('pull-material-complete'),
            data: {
                pulls: model,
                jobId,
                jobItemId
            },
            success: function () {
                let jifmId = $('#pull_finishing_material_modal').data('jifm-id');
                let $btn = $(`.open-fm-pull-modal[data-jifm-id="${jifmId}"]`);
                // Check if all rows in the modal are now fully pulled
                let allFullyPulled = true;
                $('#pull_finishing_material_modal .pull-material-row').each(function () {
                    let pulled = parseInt($(this).find('input').val()) || 0;
                    let remaining = parseInt($(this).find('.atp-display').text().trim()) || 0;
                    if (pulled < remaining) allFullyPulled = false;
                });
                // Remove any existing indicator next to this button
                $btn.siblings('.fm-pull-indicator').remove();
                if (allFullyPulled) {
                    $btn.after('<i class="fa-solid fa-circle-check ms-3 fs-18 fm-pull-indicator" style="color: #28a745;" title="Fully Pulled"></i>');
                    // Auto-check the Complete checkbox on this finishing material's service row
                    let $fmCb = $btn.closest('.service-entry').find('.service-complete-cb');
                    if ($fmCb.length && !$fmCb.is(':checked')) {
                        $fmCb.prop('checked', true);
                        CompleteToggle($fmCb);
                    }
                } else {
                    $btn.after('<i class="fa-solid fa-circle-half-stroke ms-3 fs-18 fm-pull-indicator" style="color: #f0ad4e;" title="Partially Pulled"></i>');
                }
            },
            error: function () {
                alert('Error pulling finishing material');
            }
        });
    }
};

function OpenFinishingMaterialPullModal() {
    let jifmId = $(this).attr('data-jifm-id');
    $('#pull_finishing_material_modal').data('jifm-id', jifmId);
    $('#pull_finishing_material_modal').modal('show');
};

function LoadInkPullModal() {
    $('#pull_ink_modal .pulls').html('');

    $.ajax({
        type: 'GET',
        url: ProcessUrl('get-inks'),
        success: function (data) {
            try {
                let d = JSON.parse(data);

                for (let i = 0; i < d.length; i++) {
                    //label each ink category so inks from different presses can be told apart
                    if (i === 0 || d[i].CategoryTitle !== d[i - 1].CategoryTitle) {
                        $('#pull_ink_modal .pulls').append(
                            `<div class="ff-poppins fs-16 fw-bold text-uppercase ${i === 0 ? '' : 'mt-4'}">${d[i].CategoryTitle}</div>`
                        );
                    }

                    let inkName = d[i].FriendlyName ? `${d[i].FriendlyName} (${d[i].Title})` : d[i].Title;
                    let stock = d[i].MaterialInventory.Stock != null ? d[i].MaterialInventory.Stock : 'N/A';

                    $('#pull_ink_modal .pulls').append(
                        `<div class="ink-item d-flex flex-row align-items-center mt-2" data-material-id="${d[i].Id}">
                            <div class="d-flex flex-column">
                                <label># Pulled</label>
                                <input type="number" class="ink-pull-amt ff-dm-sans fs-14" />
                            </div>
                            <span class="ff-poppins fs-18 fw-bold ps-3">${inkName}</span>
                            <span class="ff-lato fs-16 ps-3">Current Stock: ${stock}</span>
                        </div>`
                    );
                }
            } catch (e) {
                console.log('Error on parsing data...');
                console.log(e);
            }
        },
        error: function () {
            console.log('request error when getting inks');
        }
    });
};

$(function () {
    updateTimeTrackingTimers();
    setInterval(updateTimeTrackingTimers, 1000);

    $(document).ajaxSend(function () {
        $('.body-content button,.body-content input').prop('disabled', true);
    });

    $(document).ajaxComplete(function () {
        $('.body-content button,.body-content input').prop('disabled', false);
    });
})
    .on('click', '.start-task-btn', StartServiceCheck)
    .on('click', '.stop-task-btn', StopTask)
    .on('click', '.service-complete-cb', CompletePromptCheck)
    .on('click', '.confirm-multiple-services', StartService)
    .on('click', '.tt-goto-item', GoToItem)
    .on('click', '.end-service-btn', EndService)
    .on('click', '.qa-service-link', OpenQAModal)
    .on('click', '.pre-complete-prompt-cancelled', PreCompletePromptCancel)
    .on('click', '.pull-material-complete-btn', CompletePullMaterial)
    .on('click', '.pull-ink-complete-btn', CompletePullInk)
    .on('click', '.open-fm-pull-modal', OpenFinishingMaterialPullModal)
    .on('click', '.pull-fm-complete-btn', CompletePullFinishingMaterial)
    .on('show.bs.modal', '#pull_material_modal', LoadMaterialPullModal)
    .on('show.bs.modal', '#pull_finishing_material_modal', LoadFinishingMaterialPullModal)
    .on('show.bs.modal', '#pull_ink_modal', LoadInkPullModal)
    .on('click', '.start-general-task-btn', StartGeneralTask)
    .on('click', '.stop-general-task-btn', StopGeneralTask)
    .on('click', '.gen-task-collapser', CollapseUIHandler);