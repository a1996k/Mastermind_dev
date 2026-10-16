var activeTimeParameterParent = '';
var activeIdSelector = '';

function iterateProperties(obj) {
    $('#jsonFields').html('');
    for (let key in obj) {
        if (obj.hasOwnProperty(key)) {
            if (typeof obj[key] === 'object' && obj[key] !== null) {
                activeTimeParameterParent = key;
                iterateProperties(obj[key]); // Recursive call for nested objects
            } else {
                //output to view
                $('#jsonFields').append(
                    `<div class="field-binder-col-flex fs-3 param-entry">
                        <span class="param-name">${key}</span>
                        <input type="text" name="${key}" value="${obj[key]}" class="param-value" />
                    <div>`);
            }
        }
    }
};

function RenderInlineTimingParameters() {
    if (typeof timingParamsJson === 'undefined' || !timingParamsJson) return;

    var $container = $('#inline_timing_params');
    if ($container.length === 0) return;

    $container.html('');

    try {
        var obj = timingParamsJson; // Already a JavaScript object, no parsing needed

        for (let key in obj) {
            if (obj.hasOwnProperty(key)) {
                if (typeof obj[key] === 'object' && obj[key] !== null) {
                    activeTimeParameterParent = key;
                    // Iterate over the nested parameters
                    for (let paramKey in obj[key]) {
                        if (obj[key].hasOwnProperty(paramKey)) {
                            $container.append(
                                `<div class="field-binder-flexxy justify-content-lg-start">
                                    <label>${paramKey}</label>
                                    <input type="text" name="timing_${paramKey}" data-param-key="${paramKey}" value="${obj[key][paramKey]}" class="inline-timing-param" />
                                </div>`);
                        }
                    }
                }
            }
        }
    } catch (e) {
        console.log('Error rendering timing parameters:', e);
    }
};

function CollectInlineTimingParameters() {
    var params = {};
    $('#inline_timing_params .inline-timing-param').each(function () {
        params[$(this).data('param-key')] = $(this).val();
    });
    return params;
};

function SaveInlineTimingParameters() {
    var params = CollectInlineTimingParameters();
    if (Object.keys(params).length === 0) return $.Deferred().resolve().promise();

    let recordType = 'QUOTE';
    let idSelector = '#edit_service_modal #QuoteItemService_Id';

    if (window.location.href.toLowerCase().includes('job')) {
        recordType = 'JOB';
        idSelector = '#edit_service_modal #JobItemService_Id';
    }

    return $.ajax({
        type: 'POST',
        url: ProcessUrl('update-timing-parameters'),
        data: {
            id: $(idSelector).val(),
            config: JSON.stringify(params),
            modelType: activeTimeParameterParent,
            itemType: recordType
        }
    });
};

function GetServiceTimingParameters() {

    let recordType = 'QUOTE';
    let idSelector = '#edit_service_modal #QuoteItemService_Id';

    if (window.location.href.toLowerCase().includes('job')) {
        recordType = 'JOB';
        idSelector = '#edit_service_modal #JobItemService_Id';
    }
    $.ajax({
        type: 'POST',
        url: ProcessUrl('get-timing-formula-params'),
        data: {
            id: $(idSelector).val(),
            recordType: recordType
        },
        success: function (data) {
            try {
                let res = JSON.parse(data);
                activeIdSelector = idSelector;
                iterateProperties(res);
            } catch (e) { }
        }
    });
};

function SaveTimingParameters() {
    let formContentArray = $('#timing_parameter_form').serializeArray();
    let recordType = 'QUOTE';

    let formContent = {};
    formContentArray.forEach(function (item) {
        formContent[item.name] = item.value;
    });

    if (window.location.href.toLowerCase().includes('job')) {
        recordType = 'JOB';
    }
    $.ajax({
        type: 'POST',
        url: ProcessUrl('update-timing-parameters'),
        data: {
            id: $(activeIdSelector).val(),
            config: JSON.stringify(formContent),
            modelType: activeTimeParameterParent,
            itemType: recordType
        }
    });
};

$(function () {
})
    .on('click', '.param-wrapper,.view-parameters', GetServiceTimingParameters)
    .on('click', '.save-timing-params-btn', SaveTimingParameters);