var currentMenuAction;
var comparerBase;

let sidemenuTimerData = [];

let intervalTrackers = [];

var qaowl;

// Function to update each timer
function updateTimers() {
    const now = new Date();

    sidemenuTimerData = sidemenuTimerData.map(timer => ({
        ...timer,
        startTime: new Date(timer.startTime) // Convert string to Date
    }));

    sidemenuTimerData.forEach(timer => {
        // Check if startTime is a valid Date
        if (isNaN(timer.startTime)) {
            console.error(`Invalid startTime for timer id ${timer.id}`);
            return; // Skip this timer if startTime is invalid
        }

        const elapsedMs = now - timer.startTime;

        // Calculate hours, minutes, and seconds
        const hours = Math.floor((elapsedMs / (1000 * 60 * 60)) % 24);
        const minutes = Math.floor((elapsedMs / (1000 * 60)) % 60);
        const seconds = Math.floor((elapsedMs / 1000) % 60);

        // Format the time as HH:MM:SS
        const formattedTime =
            String(hours).padStart(2, '0') + ":" +
            String(minutes).padStart(2, '0') + ":" +
            String(seconds).padStart(2, '0');

        // Update the display for each timer by ID
        const timerElement = document.getElementById(`td_${timer.id}`);
        if (timerElement) {
            timerElement.textContent = formattedTime;
        } else {
            console.warn(`No element found for timer id ${timer.id}`);
        }
    });
}

function openNav() {
    $('body').removeClass('sidenav-closed').addClass('sidenav-open');
    $('.sidenav').removeClass('sidenav-closed').addClass('sidenav-open');
    SaveSideMenuState();
}

function closeNav() {
    $('body').removeClass('sidenav-open').addClass('sidenav-closed');
    $('.sidenav').removeClass('sidenav-open').addClass('sidenav-closed');
    SaveSideMenuState();
}

/* BUG/FEATURE REQUEST RELATED */
function ResetBugFormDefaults() {
    $('.bug-desc').val('');
    $('.bug-severity').val(1);
    $('.bug-priority').val(1);
    $('.bug-module').val(1);
    $('.bug-environment').val(1);
    $('.bug-str').val('');
    $('.bug-eb').val('');
    $('.bug-ab').val('');
};

function GetBug() {
    let bug = {};
    bug.Description = $('.bug-desc').val();
    bug.Severity = $('.bug-severity').val();
    bug.Priority = $('.bug-priority').val();
    bug.Module = $('.bug-module').val();
    bug.Environment = $('.bug-environment').val();
    bug.StepsToReproduce = $('.bug-str').val();
    bug.ExpectedBehavior = $('.bug-eb').val();
    bug.ActualBehavior = $('.bug-ab').val();

    return bug;
};
function SubmitBug() {
    let bug = GetBug();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('submit-bug'),
        data: {
            bug
        },
        success: function (data) {
            try {
                let res = JSON.parse(data);
                alert('Bug Created with Id #' + res);

                ResetBugFormDefaults();
            } catch (e) { }
        }
    })
}

function ResetFeatureRequestFormDefaults() {
    $('.fr-desc').val('');
};

function GetFeatureRequest() {
    let fr = {};
    fr.Description = $('.fr-desc').val();

    return fr;
};
function SubmitFeatureRequest() {
    let featureRequest = GetFeatureRequest();

    $.ajax({
        type: 'POST',
        url: ProcessUrl('submit-feature'),
        beforeSend: ResetFeatureRequestFormDefaults,
        data: {
            featureRequest
        },
        success: function (data) {
            try {
                let res = JSON.parse(data);
                alert('Feature Request Created with Id #' + res);
            } catch (e) { }
        }
    })
};
/* END BUG/FEATURE RELATED */

function roundToDecimalPlace(num, decimalPlaces) {
    const factor = Math.pow(10, decimalPlaces);
    return Math.round(num * factor) / factor;
}

function UpdateMarkupPercentage(override) {
    $('.markup-input').val(override);
    $('.markup-input').trigger('blur');
};

function getSelectValueText(selectId) {
    if (selectId.indexOf("#") == -1) {
        selectId = "#" + selectId;
    }

    var select = document.getElementById(selectId);
    var selectedOption = select.options[select.selectedIndex];
    return selectedOption.text;
};
function SetActiveLink() {
    let curPage = window.location.pathname;

    $('a.nav-link').each(function () {
        if ($(this).attr('href') === curPage) {
            $(this).addClass('active');
        }
    });
};

function OpenModal() {
    var $m = $(this);
    if (typeof $m.attr('data-modal-to-open') !== 'undefined') {
        $($m.attr('data-modal-to-open')).modal('show');
    }
};
function CloseModal() {
    var $m = $(this);
    if (typeof $m.attr('data-modal-to-close') !== 'undefined') {
        if ($m.attr('data-modal-to-close') === '#') {
            //close the open modal
            $('.modal.show').modal('hide');
        } else {
            //close a specific modal
            $($m.attr('data-modal-to-close')).modal('hide');
        }
    }
};

function HasUnsavedChanges() {
    return $('[data-changed="True"]').length > 0;
};

/* assumes there are unsaved changes if called */
function UnsavedChangesHandler() {
    $('[data-changed="True"]').each(function () {
        var $e = $(this);

        if (alert('Unsaved Changes!')) {
            var recordId = $('input[name="Id"]').val();
            $.post('DeleteQuote/' + recordId);
        }
    });
};

function SetAsModified() {
    $(this).attr('data-changed', 'True');
};

function Submitted(evt) {
    return evt.type === 'submit';
};

function FormSubmitHandler(evt) {
    if (HasUnsavedChanges() && !Submitted(evt)) {
        evt.preventDefault();
        UnsavedChangesHandler(evt);
    }
};

function ClearChecks(context) {
    var ctx;
    if (typeof context !== 'undefined') {
        ctx = context + ' input[type="checkbox"]';
    } else {
        ctx = 'input[type="checkbox"]';
    }
    $(ctx).each(function () {
        $(this).prop('checked', false);
    });
};

function ProcessChecks(context) {
    var arr = new Array();
    var ctx;
    if (typeof context !== 'undefined') {
        ctx = context + ' input[type="checkbox"]:checked';
    } else {
        ctx = 'input[type="checkbox"]:checked';
    }

    $(ctx).each(function () {
        arr.push($(this).attr('data-record-id'));
    });

    return arr;
};

function RemoveFromTable() {
    //get the tr that contains the clicked button
    var $tr = $(this).closest('tr');
    //remove the tr from the table
    $tr.remove();
};

/* this function simply hides the item table (the button to add a row still shows, though) if there are
    no rows in the table, otherwise it shows. expected to be called by Add/Remove Row functions in quotes/estimates/jobs.js */
function TableRowRemoveAddHandler(table_id) {
    var tid = ('#' + table_id);
    var rowSelector = tid + ' tbody tr';

    if ($(rowSelector).length > 0) {
        $(tid).show();
    } else {
        $(tid).hide();
    }
};

function NonTableRowRemoveAddHandler() {
    var did = ('#job_items');
    var rowSelector = did + ' .item-row';

    if ($(rowSelector).length > 0) {
        $(did).show();

        if ($(rowSelector).length === 1) {
            // Only auto-fill the lone item's description from the job description when it's
            // still blank (e.g. a brand-new single-item quote/job). Never clobber a description
            // that already has content: deleting items down to one used to replace the
            // surviving item's real description with the job description.
            var $loneDesc = $(rowSelector).find('.item-description');
            if ($.trim($loneDesc.val()) === '') {
                var jobDesc = $('form textarea[id*="Description"]').val();
                $loneDesc.val(jobDesc);
            }
            $(rowSelector).find('.service-list').sortable();
        }
    } else {
        $(did).hide();
    }

    var $pccf = $('#job_items .item-row:last-child').find('.press-color-count-front input');
    var $pccb = $('#job_items .item-row:last-child').find('.press-color-count-back input');

    if ($pccf.val() === '') {
        $pccf.val(4);
    }

    if ($pccb.val() === '') {
        $pccb.val(0);
    }

    //ScrollPageToBottom();
};

function ScrollPageToBottom(delay = 1500) {
    $('html, body').animate({ scrollTop: $(document).height() }, delay);
};

function JobDisplayGetDeliveryTicket() {
    var jId = $('.clicked-job').attr('data-record-id');
    window.location = ProcessUrl('delivery-ticket/' + jId);
};

function JobDisplayViewJob() {
    var jId = $('.clicked-job').attr('data-record-id');

    window.location = ProcessUrl('Job/View/' + jId);
};

function JobDisplayViewTicket() {
    var jId = $('.clicked-job').attr('data-record-id');

    window.location = ProcessUrl('GetJobTicket/' + jId);
};

function JobDisplayEditJob() {
    var jId = $('.clicked-job').attr('data-record-id');

    window.location = ProcessUrl('Job/Edit/' + jId);
};

function LedSelect() {
    var $clickedLed = $(this);
    var $cbPartner = $($clickedLed.attr('data-cb-partner'));
    var curVal = $cbPartner.is(':checked');
    curVal = !curVal;

    $cbPartner.prop('checked', curVal);

    if (curVal === true) {
        $clickedLed.addClass('selected');
    } else {
        $clickedLed.removeClass('selected');
    }
};

//removes the parent (and all children elements)
function RemoveParent() {
    $(this).parent().remove();
};

function RemoveParentListItem() {
    $(this).parents('li').remove();
};

function Login() {
    var oId = $('#operator_un').val();
    var oPwd = $('#operator_pcode').val();

    //console.log(oPwd);
    $.ajax({
        type: 'POST',
        url: ProcessUrl('login'),
        data: {
            userName: oId,
            passcode: oPwd
        },
        success: function (data) {
            var res = JSON.parse(data);
            //set welcome msg
            //show logged-in-wrapper
            //hide login button
            $('.u-welcome-msg').text(res.WelcomeMessage);
            $('.login-btn').hide();
            $('.logged-in-wrapper').show();
            $('#login #operator_un').val('');
            $('#login').modal('hide');
            window.location = ProcessUrl('PrintDashboard');
        },
        error: function (err) {
            console.log(err);
        }
    });
};

//gets the user id from the ActiveUser cookie and sends it to the backend. a new expired cookie is returned. easier this way.
function Logout() {
    let uCookie = getCookie('ActiveUser');
    $.ajax({
        type: 'POST',
        url: ProcessUrl('logout'),
        data: {
            userId: uCookie
        },
        beforeSend: function () {
            //"delete" all cookies
            var cookies = document.cookie.split(";");

            for (var i = 0; i < cookies.length; i++) {
                var cookie = cookies[i];
                var eqPos = cookie.indexOf("=");
                var name = eqPos > -1 ? cookie.substr(0, eqPos) : cookie;
                document.cookie = name + "=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/";
            }
        },
        success: function () {
            $('.logged-in-wrapper').hide();
            $('.login-btn').show();
            $('#login #operator_un').val('');
            $('#login').modal('hide');
            window.location = ProcessUrl('?initNav');
        }
    });
};

function UIUpdate_RemoveMarcusScreens() {
    $('.for-marcus').remove();
    $('.record-inspector').remove();
    $('.email-tester').remove();
};

function getCookie(cname) {
    let name = cname + "=";
    let decodedCookie = decodeURIComponent(document.cookie);
    let ca = decodedCookie.split(';');
    for (let i = 0; i < ca.length; i++) {
        let c = ca[i];
        while (c.charAt(0) == ' ') {
            c = c.substring(1);
        }
        if (c.indexOf(name) == 0) {
            return c.substring(name.length, c.length);
        }
    }
    return "";
};

function CheckUserCookie() {
    let uCookie = getCookie('ActiveUser');

    if (uCookie !== '') {
        $('.login-btn').hide();
    }
};
function LoginHelper(evt) {
    if (evt.keyCode === 13) {
        $('.execute-login').trigger('click');
    }
};

function DocumentDeletionHandler() {
    //if the length of checked .del-marker-cb elements is greater than 0 then show the .delete-docs-btn button otherwise hide it
    let $delCbs = $('.del-marker-cb:checked');
    if ($delCbs.length > 0) {
        $('.delete-docs-btn').show();
    } else {
        $('.delete-docs-btn').hide();
    }
};

function FocusUserInput() {
    $('#operator_un').focus();
};

function CalculateNewMarkup(newPercent) {
    const markupAmount = (40 * (newPercent - 40)) / 100;
    const markedUpValue = 40 + markupAmount;

    return markedUpValue;
};

//chatgpt refactored
function MoveRowUp() {
    let $btn = $(this);
    let $row = $btn.closest('.item-row');
    let isSub = $row.hasClass('sub-item');

    if (isSub) {
        // Moving a sub-item row, ensuring it stays within its parent group
        let $prevSibling = $row.prev('.item-row.sub-item');
        if ($prevSibling.length) {
            $row.insertBefore($prevSibling); // Move the sub-item up within the same parent group
        }
    } else {
        // Identify the set of elements to move (the main item row and its sub-items, if any)
        let $setToMove = $row.add($row.nextUntil('.item-row:not(.sub-item)'));

        // Moving a main item row and its sub-items
        let $targetRow = $row.prevAll('.item-row:not(.sub-item)').first(); // Target for insertion

        if ($targetRow.length) {
            // Case for moving above another main item row
            $setToMove.insertBefore($targetRow);
        } else {
            // If there's no previous main item row, no action is needed as it's already the first item
        }
    }

    UpdateItemOrdinals();
};

//chatgpt refactored
function MoveRowDown() {
    let $btn = $(this);
    let $row = $btn.closest('.item-row');
    let isSub = $row.hasClass('sub-item');

    if (isSub) {
        // Moving a sub-item row, ensuring it stays within its parent group
        let $nextSibling = $row.next('.item-row.sub-item');
        if ($nextSibling.length) {
            $row.insertAfter($nextSibling); // Move the sub-item down within the same parent group
        }
    } else {
        // Identify the set of elements to move (the main item row and its sub-items, if any)
        let $setToMove = $row.add($row.nextUntil('.item-row:not(.sub-item)'));

        // Find the next main item row after the current item and its sub-items
        let $nextMainRow = $setToMove.last().nextAll('.item-row:not(.sub-item)').first();

        if ($nextMainRow.length) {
            // If there's a next main item, we find if it has sub-items to determine the correct insertion point
            let $nextMainSubItems = $nextMainRow.nextUntil('.item-row:not(.sub-item)');
            if ($nextMainSubItems.length) {
                // If the next main row has sub-items, insert after the last sub-item of the next main row
                $setToMove.insertAfter($nextMainSubItems.last());
            } else {
                // If the next main row has no sub-items, insert directly after the next main row
                $setToMove.insertAfter($nextMainRow);
            }
        }
        // If there's no next main item, no action is needed as it's already the last item
    }

    UpdateItemOrdinals();
};

function UpdateItemOrdinals() {
    let ord = 1;
    let idArr = [];
    let ordArr = [];
    let itypeArr = [];

    let iTypebase = 'JOB';
    if (window.location.href.toUpperCase().indexOf('QUOTE') > -1) {
        iTypebase = 'QUOTE';
    }
    $('.item-row').each(function () {
        let $row = $(this);
        $row.find('.ordinal-display').html('Item ' + ord);
        let $inp = $row.find('.itemordinal');
        $inp.val(ord);
        idArr.push($row.attr('data-record-id'));
        ordArr.push(ord);
        if ($row.hasClass('o-item-row')) {
            itypeArr.push(('O-' + iTypebase));
        } else {
            itypeArr.push(iTypebase);
        }
        ord++;
    });

    //console.log(idArr);
    //console.log(ordArr);
    //console.log(itypeArr);

    $.ajax({
        type: 'POST',
        url: ProcessUrl('update-item-ordinals'),
        data: {
            itemIds: idArr,
            ordinals: ordArr,
            itemtypes: itypeArr
        }
    });
};

function ShowCalcSpinner() {
    $('.pricing-wrapper').addClass('no-pointer-events');
    document.getElementById('spinner').style.display = 'flex';
};

function HideCalcSpinner() {
    $('.pricing-wrapper').removeClass('no-pointer-events');
    document.getElementById('spinner').style.display = 'none';
};

function createInput(key, value, containerId = 'jsonFields', prefix = 'default') {
    //replace spaces with underscores
    key = key.replace(/\s+/g, '_');

    var container = document.getElementById(containerId);
    var input = document.createElement("input");
    var label = document.createElement("label");
    var regex = new RegExp(prefix + "\.", "g");  // Use RegExp to dynamically replace the prefix
    var readableKey = key.replace(/_/g, " ").replace(regex, ""); // Now using the regex
    label.innerText = readableKey + ": ";
    input.type = "text";
    input.name = key;
    input.value = value;
    input.setAttribute('data-original-name', key);

    container.appendChild(label);
    container.appendChild(input);
    container.appendChild(document.createElement("br"));
}

function processObject(obj, prefix = '') {
    for (var key in obj) {
        if (obj.hasOwnProperty(key) && obj[key] !== null && typeof obj[key] === 'object') {
            processObject(obj[key], prefix + key + '.');
        } else {
            createInput(prefix + key, obj[key]);
        }
    }
}

// Function to get a specific cookie by name
function getCookie(name) {
    var value = "; " + document.cookie;
    var parts = value.split("; " + name + "=");
    if (parts.length === 2) return parts.pop().split(";").shift();
};

function ToggleQuoteJobField() {
    let $btn = $(this);
    let value = $btn.attr('value');
    if (value === 'job') {
        $('.navbar-job-lookup').show();
        $('.navbar-quote-lookup').hide();
    }
    else {
        $('.navbar-job-lookup').hide();
        $('.navbar-quote-lookup').show();
    }
};

function SaveSideMenuState() {
    const userId = getCookie('ActiveUser');
    if (!userId) return;

    const state = {
        config: {
            isOpen: !$('body').hasClass('sidenav-closed')
        }
    };

    localStorage.setItem(`sideMenuState_${userId}`, JSON.stringify(state));
};

function GetSideMenuState() {
    const userId = getCookie('ActiveUser');
    if (!userId) return;

    const state = JSON.parse(localStorage.getItem(`sideMenuState_${userId}`)) || {
        config: {
            isOpen: true
        }
    };

    return state;
};

function SetSideMenuState(state) {
    if (!state) {
        state = GetSideMenuState();
    }

    try {
        if (state.config.isOpen) {
            $('.sidenav').addClass('sidenav-open');
            $('body').addClass('sidenav-open');
            setHeaderWidth();
        } else {
            $('.sidenav').addClass('sidenav-closed');
            $('body').addClass('sidenav-closed');
            setHeaderWidth();
        }
    } catch (e) {
        //not defined, which is ok
    }
}

function GoToHomepage() {
    window.location = ProcessUrl('');
};

function GoToRecordInspector() {
    window.location = ProcessUrl('record-inspector');
};

function GoToMarcus() {
    window.location = ProcessUrl('Administration/MarcusAdminScreen');
};

function TestEmail() {
    window.location = ProcessUrl('email-tester');
}

function ChangeDetected(from, to) {
    console.log('Field has been changed');
    console.log('From: ' + from);
    console.log('To: ' + to);

    //ShowSaveButton();
}

function ChangeDetector() {
    let $ele = $(this);
    comparerBase = $ele.val();
};

function ChangeDetectorCheck() {
    let $ele = $(this);
    let val = $ele.val();

    if (val !== comparerBase) {
        ChangeDetected(comparerBase, val);
    }
};

function LoadBugForm() {
    $.ajax({
        type: 'GET',
        url: ProcessUrl('bug-report'),
        success: function (data) {
            try {
                let res = JSON.parse(data);
                $('#bug_modal .modal-body').html(res.html);
            } catch (e) { }
        }
    });
};

function LoadFeatureRequestForm() {
    $.ajax({
        type: 'GET',
        url: ProcessUrl('feature-request'),
        success: function (data) {
            try {
                let res = JSON.parse(data);
                $('#feature_request_modal .modal-body').html(res.html);
            } catch (e) { }
        }
    });
};

function MultiselectDropdownSearch() {
    let $dd = $(this);
}

function FilterMultiselectDropdown() {
    //get source/targets
    let $searchField = $(this);

    let $dropdown = $searchField.closest('.msl-dropdown-container');
    let $selectList = $dropdown.find('.msl-dropdown-list');
    let searchTerm = $searchField.val();

    $selectList.children('li').each(function (index, item) {
        // Use $(item) to wrap the raw DOM element in jQuery
        let labelText = $(item).find('label').text().toLowerCase(); // Case-insensitive comparison
        $(item).css('display', labelText.includes(searchTerm.toLowerCase()) ? 'block' : 'none');
    });
};

function ToggleMultiselectCheckboxes() {
    //function to check/uncheck all visible items when the top most checkbox is checked/unchecked
    let $cb = $(this);
    let isChecked = $cb.is(':checked');
    let $selectList = $cb.parents('.msl-dropdown-container').find('.msl-dropdown-list');
    let $items = $selectList.find('li');

    $items.each(function () {
        let $thisCb = $(this);
        //alter the state of the select list checkbox if the checkbox is visible (filter assist)
        if ($thisCb.is(':visible')) {
            $thisCb.find('input[type="checkbox"]').prop('checked', isChecked);
        }
    });
}

function GoToGeoTest() {
    window.location = ProcessUrl('Dev/GeoTest');
};

function PositionPricingWrapper() {
    let constantOffset = 20;
    //.pricing-wrapper top = navbar height
    let navHeight = $('nav').height();
    navHeight = navHeight + constantOffset;
    $('.pricing-wrapper').css('top', navHeight);
};

function setHeaderWidth() {
    //console.log("Starting setHeaderWidth function");

    // Get .edit-job-wrapper, or if it doesn't exist, get .edit-quote-wrapper
    const wrapper = document.querySelector('.edit-job-wrapper') || document.querySelector('.edit-quote-wrapper');
    if (!wrapper) {
        //console.log("No wrapper found: neither .edit-job-wrapper nor .edit-quote-wrapper exists.");
        return;
    }
    //console.log("Wrapper found:", wrapper.classList.contains('edit-job-wrapper') ? '.edit-job-wrapper' : '.edit-quote-wrapper');

    // Get #mySidenav element
    const sidenav = document.querySelector('#mySidenav');
    if (!sidenav) {
        //console.log("#mySidenav not found.");
        return;
    }
    //console.log("#mySidenav found");

    // Use GetSideMenuState to retrieve the sidenav state
    const sidenavState = GetSideMenuState();
    const isSidenavOpen = sidenavState?.config?.isOpen ?? true; // Defaults to open if state not found

    // Set sidenavWidth based on its state
    const sidenavWidth = isSidenavOpen ? 250 : 50;
    //console.log("Sidenav state:", isSidenavOpen ? 'open' : 'closed', "Width:", sidenavWidth);

    // Determine if .pricing-wrapper exists and set pricingWidth accordingly
    const pricingWrapper = document.querySelector('.pricing-wrapper');
    const pricingWidth = pricingWrapper ? 220 : 0;
    //console.log(pricingWrapper ? ".pricing-wrapper found, using width 220px" : ".pricing-wrapper not found, ignoring width");

    // Get the width of the window
    const windowWidth = window.innerWidth;

    // Calculate and set the width of the wrapper to (window - sidenav - pricing)
    const newWidth = windowWidth - sidenavWidth - pricingWidth;
    //console.log("Calculated new width for wrapper:", newWidth);

    wrapper.style.width = `${newWidth}px`;
    //console.log("Wrapper width set to:", `${newWidth}px`);
};

function DynamicServiceStop() {
    let $stopBtn = $(this);
    let rId = $stopBtn.attr('data-record-id');
    let $parentCard = $stopBtn.parents('.sm-tt-wrapper');

    //new for stopping on time tracking page
    let tdId = $stopBtn.parent().find('.timer-display').attr('id');
    let ttId = tdId.replace('td', 'mt');
    let serviceId = ttId.replace('mt_', '');

    let ttStr = '#' + ttId;

    $.ajax({
        type: 'POST',
        url: ProcessUrl('d-stop-service'),
        data: {
            recordId: rId
        },
        success: function () {
            //ui update
            $parentCard.fadeOut('fast', function () {
                $parentCard.remove();
                alert('Time stopped');
                UpdateSideMenuCardIndexes();
            });

            //ensure a visible timer exists (prevent error for non-existent function)
            if ($(ttStr).length) {
                //stop timer (ttId)
                stopTimer(serviceId);
                $(ttStr).parent().find('.stop-button').hide();
                $(ttStr).parent().find('.start-button').show();
            }
        },
        error: function (msg) {
            console.log(msg);
            alert(msg);
        }
    })
};

function UpdateSideMenuCardIndexes() {
    // Select all cards
    const cards = document.querySelectorAll('.sm-tt-wrapper');

    // Loop through each card to set the dynamic index
    cards.forEach((card, index) => {
        card.style.setProperty('--card-index', index); // Assign index for dynamic positioning
    });
};

function qaRequiredFieldsFilledOut($clickedBtn) {
    //if $clickedBtn doesn't have a data-radio-id attribute then we don't need to check, so return true
    if ($clickedBtn.is('[data-radio-id]')) {
        let rBtns = $('input[type="radio"][name="' + $clickedBtn.attr('data-radio-id') + '"]:checked');
        if (rBtns.length > 0) {
            return true;
        }
        else {
            return false;
        }
    } else {
        return true;
    }
};

function NextQASlide() {
    let $btn = $(this);
    let pwCheck = $btn.hasClass('check-pw') && !$('#second_not_available').is(':checked');

    if (qaRequiredFieldsFilledOut($btn)) {
        // If operator failed, skip secondary sign-off and submit directly
        if ($btn.hasClass('update-first-check-endtime') &&
            $('#qa_check_modal input[name="operator_result"]:checked').val() === 'fail') {
            // Set first check end time before building save model
            // (UpdateFirstCheckEndTime handler hasn't fired yet)
            $('input[name="firstCheckEndtime"]').val(new Date().toISOString());

            let saveModel = GetQASaveModel();
            saveModel.SecondApprover = -1;
            saveModel.SecondApproverPass = false;

            $.ajax({
                type: 'POST',
                url: ProcessUrl('qa-complete'),
                data: { qa: saveModel },
                success: function (data) {
                    $('#qa_check_modal').modal('hide');
                    $('.timer-display#td_' + saveModel.JobItemService).parents('.sm-tt-wrapper').remove();
                    alert('QA Check complete');
                }
            });
            return;
        }

        if (pwCheck) {
            $.ajax({
                type: 'POST',
                url: ProcessUrl('qa-pw-check'),
                data: {
                    pw: $('#qa_check_modal input[name="checker_password"]').val(),
                    selectedUser: $('#qa_check_modal select[name="QAChecker"]').val()
                },
                success: function (data) {
                    try {
                        let res = JSON.parse(data);
                        if (res.accepted) {
                            qaowl.data('owl-carousel').next();
                        } else {
                            //inform user of incorrect password
                            alert('Invalid credentials');
                        }
                    } catch (e) {
                        console.log(e);
                    }
                }
            });
        } else {
            qaowl.data('owl-carousel').next();
        }
    }
};
function PrevQASlide() {
    qaowl.data('owl-carousel').prev();
};

function LoadQAJobDetails() {
    $.ajax({
        type: 'GET',
        url: ProcessUrl('get-existing-qa-job-details'),
        success: function (data) {
            try {
                let res = JSON.parse(data);
                let $qaModal = $('#qa_check_modal');

                $qaModal.find('.qa-job-number').html(res.JobNumber);
                $qaModal.find('.qa-item-ordinal').html(res.Ordinal);
                $qaModal.find('.stock-name').html(res.MaterialName);
                $qaModal.find('.qa-print-notes').html(res.PrintNotes);
                $qaModal.find('.material-run-size').html(res.MaterialRunSize);
                $qaModal.find('#current_jis_id').val(res.JobItemService);

                $qaModal.find('#qa_prompt_time').val(new Date().toISOString());

                // Toggle print vs general QA questions
                let isPrintQA = res.IsPrintQA;
                if (isPrintQA) {
                    $qaModal.find('.print-qa-questions').show();
                    $qaModal.find('.general-qa-questions').hide();
                } else {
                    $qaModal.find('.print-qa-questions').hide();
                    $qaModal.find('.general-qa-questions').show();
                    $qaModal.find('.qa-item-description').html(res.ItemDescription);
                    $qaModal.find('.qa-service-title').html(res.ServiceTitle);
                }

                //if the user is not allowed to bypass QA, remove the bypass checkbox
                if (!res.allowQABypass) {
                    $('#qa_check_modal .secondary-check-row').remove();
                }

                //remove any matching user from select#QAChecker
                $qaModal.find('select#QAChecker option[value="' + res.userId + '"]').remove();
            } catch (e) {
                console.log(e);
            }
        }
    });
};

function GetQAApprovalJson() {
    let reses = new Array();

    //get slides 1 & 3 which contain questions (only visible q-blocks)
    $('.slide-1,.slide-3').each(function () {
        let $slide = $(this);

        $slide.find('.q-block:visible').each(function () {
            let $block = $(this);
            let qJson = {};

            qJson.Question = $block.find('.q-text').text();
            qJson.Response = $block.find('input[type="radio"]:checked').attr('value') === 'yes';
            qJson.Notes = $block.find('textarea').val();

            reses.push(qJson);
        });
    });
    console.log(reses);
    return reses;
};

function GetQASaveModel() {
    let model = {};
    let $qaModal = $('#qa_check_modal');

    model.JobItemService = $qaModal.find('#current-jis-id').val();
    model.SecondApprover = $('#second_not_available').is(':checked') ? "-1" : $('select[name="QAChecker"]').val();
    model.FirstApprovalTime = $('input[name="firstCheckEndtime"]').val();
    model.PromptTime = $('#qa_prompt_time').val();
    model.JobItemService = $('#qa_check_modal #current_jis_id').val();
    model.FirstApproverPass = $qaModal.find('input[name="operator_result"]:checked').attr('value') === 'pass';
    model.SecondApproverPass = $qaModal.find('input[name="checker_result"]:checked').attr('value') === 'pass';

    model.Responses = GetQAApprovalJson();

    return model;
}

function QAFormSubmit() {
    let $btn = $(this);
    if (qaRequiredFieldsFilledOut($btn)) {
        let saveModel = GetQASaveModel();

        $.ajax({
            type: 'POST',
            url: ProcessUrl('qa-complete'),
            data: {
                qa: saveModel
            },
            success: function (data) {
                $('#qa_check_modal').modal('hide');
                if (!saveModel.FirstApproverPass || !saveModel.SecondApproverPass) {
                    $('.timer-display#td_' + saveModel.JobItemService).parents('.sm-tt-wrapper').remove();
                }
                alert('QA Check complete');
            }
        });
    }
};

function ToggleSecondReviewer() {
    let $cb = $(this);
    let $select = $('#qa_check_modal select[name="QAChecker"]');
    let $password = $('#qa_check_modal input[name="checker_password"]');

    if ($cb.is(':checked')) {
        $select.prop('disabled', true);
        $password.prop('disabled', true);
    } else {
        $select.prop('disabled', false);
        $password.prop('disabled', false);
    }
};

function UpdateFirstCheckEndTime() {
    $('input[name="firstCheckEndtime"]').val(new Date().toISOString());
};

function NavbarGoToRecord(evt) {
    if (evt.keyCode === 13) {
        //enter pressed
        let enteredNum = $('.navbar-duo-lookup').val();
        window.location = ProcessUrl('navbar-duo-lookup') + '/' + enteredNum;
    }
};

function RemovePrice() {
    let $btn = $(this);
    let $row = $btn.closest('tr');

    let priceId = $btn.attr('data-record-id');
    let recordType = 'quote';
    //if $('#VM_Job_Job_Id) exists, set recordType to job.
    if ($('#VM_Job_Job_Id').length > 0) {
        recordType = 'job';
    }

    $.ajax({
        type: 'POST',
        url: ProcessUrl('remove-price'),
        data: {
            priceId,
            recordType
        },
        success: function () {
            DeInitPriceTable();
            //remove row
            $row.remove();
            InitPriceTable();
        },
        error: function () {
            alert('There was a problem deleting that price');
        }
    });
};

$(function () {
    // Setup MutationObserver to detect changes to #mySidenav
    const sidenav = document.querySelector('#mySidenav');
    if (sidenav) {
        const observer = new MutationObserver(() => {
            //console.log("Sidenav state changed, recalculating header width...");
            setHeaderWidth();  // Run the function whenever the sidenav changes
        });

        // Observe changes to the sidenav's attributes
        observer.observe(sidenav, { attributes: true, attributeFilter: ['style', 'class'] });
    }

    //override datatable init's to be 100 items by default
    $.extend($.fn.dataTable.defaults, {
        pageLength: 100,
        initComplete: function (settings, json) {
            // Create a DataTables API instance from the table context
            var api = this.api();

            // Locate the search input within the table's container
            var $container = $(api.table().container());
            var $searchInput = $container.find('div.dataTables_filter input');

            // Add your custom attribute
            $searchInput.attr('autocomplete', 'off');
        }
    });

    CheckUserCookie();
    String.prototype.format = String.prototype.f = function () {
        var s = this,
            i = arguments.length;

        while (i--) {
            s = s.replace(new RegExp('\\{' + i + '\\}', 'gm'), arguments[i]);
        }
        return s;
    };

    //console.log('Making modals draggable..');
    $('.modal-dialog').draggable({
        handle: '.modal-header'
    });

    //console.log('ajax for user status...');
    // Only call user-status if user is logged into Mastermind
    var activeUserCookie = getCookie('ActiveUser');
    if (activeUserCookie) {
        $.ajax({
            type: 'GET',
            url: ProcessUrl('user-status'),
            success: function (data) {
                try {
                    var res = JSON.parse(data);

                    if (!res.isMarcus) {
                        UIUpdate_RemoveMarcusScreens();
                    } else {
                        $('.record-inspector').removeClass('d-none');
                        $('.email-tester').removeClass('d-none');
                    }
                } catch (exception) { }
            }
        });
    } else {
        // No Mastermind session - hide Marcus-only screens
        UIUpdate_RemoveMarcusScreens();
    }
    //permissions
    // Get the data-permissions attribute value from the cookie
    var permissionsCookie = getCookie("Permissions");

    // Reusable permissions check — call this after any dynamic content is injected
    window.applyPermissions = function (context) {
        var cookie = getCookie("Permissions");
        var scope = context ? $(context) : $(document);
        if (cookie) {
            var perms = [];
            for (var i = 0; i < cookie.length; i += 2) {
                perms.push(cookie.substr(i, 2));
            }
            scope.find("[data-permissions]").addBack("[data-permissions]").each(function () {
                var required = $(this).attr("data-permissions");
                if (!perms.includes(required)) {
                    $(this).remove();
                } else {
                    $(this).show();
                }
            });
        } else {
            scope.find("[data-permissions]").addBack("[data-permissions]").remove();
        }
    };

    //console.log('checking permissions cookie');
    if (permissionsCookie) {
        // Parse the permissionsCookie two characters at a time
        var permissions = [];
        for (var i = 0; i < permissionsCookie.length; i += 2) {
            permissions.push(permissionsCookie.substr(i, 2));
        }

        // Select all elements with [data-permissions] attribute
        var elements = $("[data-permissions]");

        // Loop through elements and remove those not in the permissions array
        elements.each(function () {
            var elementPermission = $(this).attr("data-permissions");
            if (!permissions.includes(elementPermission)) {
                $(this).remove();
            } else {
                $(this).show();
            }
        });

        //console.log('updating login button displays');
        $('.logout-btn').addClass('logged-in');
        $('.login-btn').addClass('logged-in');
    } else {
        //console.log('updating login button displays');
        $('.login-btn').addClass('logged-out');
        $('.logout-btn').addClass('logged-out');
        $('.login-btn').show();
        $('.logout-btn').hide();
        $('[data-permissions]').remove();
    }

    $('.navbar-duo-lookup').autocomplete({
        source: function (request, response) {
            $.ajax({
                url: ProcessUrl('navbar-d-autocomplete'),
                type: 'GET',
                data: {
                    'q': request.term
                },
                success: function (data) {
                    try {
                        let res = JSON.parse(data);

                        response($.map(res, function (item) {
                            return { label: item.JobNumber, value: item.JobNumber, recordId: item.Id, recordType: item.recordType }
                        }));
                    } catch (e) {
                        console.log(e);
                    }
                }
            })
        },
        select: function (event, ui) {
            let recordId = ui.item.recordId;
            //check record type here
            if (ui.item.recordType === 'Quote') {
                window.location = (ProcessUrl('Quote/Edit/') + recordId);
            }
            if (ui.item.recordType === 'Job') {
                window.location = (ProcessUrl('Job/Edit/') + recordId);
            }
        },
        position: {
            my: "left top+20",
            at: "left bottom", // Adjust the position 20px downward
            collision: "none"
        }
    });

    qaowl = $("#qa_oc");
    qaowl.owlCarousel({
        items: 1,
        nav: false,
        dots: false,
        loop: false,
        pagination: false,
        mouseDrag: false,
        touchDrag: false,
    });

    //console.log('Setting side menu state...');
    SetSideMenuState();

    //side menu time tracking js
    UpdateSideMenuCardIndexes();
    //end side menu time tracker js
    updateTimers();
    setInterval(updateTimers, 1000);
})
    .on('click', '[data-modal-to-open]', OpenModal)
    .on('click', '[data-modal-to-close]', CloseModal)
    .on('click', 'table .remove-btn', RemoveFromTable)
    .on('click', '.toast-view-button', JobDisplayViewJob)
    .on('click', '.toast-job-ticket-button', JobDisplayViewTicket)
    .on('click', '.toast-edit-job-button', JobDisplayEditJob)
    .on('click', '.toast-delivery-ticket-button', JobDisplayGetDeliveryTicket)
    .on('click', '.led-select:not(.locked)', LedSelect)
    .on('click', '.remove-link', RemoveParent)
    .on('click', '.remove-link-li', RemoveParentListItem)
    .on('click', '.execute-login', Login)
    .on('click', '.logout-btn', Logout)
    .on('keyup', '#operator_pcode', LoginHelper)
    .on('click', '.del-marker-cb', DocumentDeletionHandler)
    .on('shown.bs.modal', '#login', FocusUserInput)
    .on('click', '.move-up-btn', MoveRowUp)
    .on('click', '.move-down-btn', MoveRowDown)
    .on('click', 'input[name="quoteJobRadio"]', ToggleQuoteJobField)
    .on('click', '.navbar-brand', GoToHomepage)
    .on('click', '.record-inspector button', GoToRecordInspector)
    .on('click', '.for-marcus button', GoToMarcus)
    .on('click', '.email-tester button', TestEmail)
    .on('show.bs.modal', '#bug_modal', LoadBugForm)
    .on('show.bs.modal', '#feature_request_modal', LoadFeatureRequestForm)
    .on('click', '.submit-bug-btn', SubmitBug)
    .on('click', '.submit-fr-btn', SubmitFeatureRequest)
    .on('keyup', '.msl-dropdown-search', FilterMultiselectDropdown)
    .on('click', '.msl-all-cb', ToggleMultiselectCheckboxes)
    .on('click', '.geo-test-item', GoToGeoTest)
    .on('click', '.dynamic-service-stop', DynamicServiceStop)
    .on('click', '#qa_check_modal .next-slide', NextQASlide)
    .on('click', '#qa_check_modal .prev-slide', PrevQASlide)
    .on('show.bs.modal', '#qa_check_modal', LoadQAJobDetails)
    .on('click', '#qa_check_modal .submit-form', QAFormSubmit)
    .on('click', '#second_not_available', ToggleSecondReviewer)
    .on('click', '.update-first-check-endtime', UpdateFirstCheckEndTime)
    .on('keyup', '.navbar-duo-lookup', NavbarGoToRecord)
    .on('click', '#priceListModal .remove-price', RemovePrice);