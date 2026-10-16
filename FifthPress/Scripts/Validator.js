var failedValidation = false;

function ValidateItem($item) {

    var val = $item.val().toString();

    //negative result
    if (val === '') {
        $item.addClass('failed-validation');
        failedValidation = true;
    }
};



function CheckFormValidation() {
    $('.failed-validation').removeClass('failed-validation');
    failedValidation = false;
    //need to check the current url
    var url = window.location.href;

    if (url.indexOf('Quote/Create') > -1 || url.indexOf('Job/Create') > -1 || url.indexOf('Quote/Edit') > -1 || url.indexOf('Job/Edit') > -1) {
        var prefix = url.indexOf('Quote') > -1 ? 'Quote' : 'Job';
        var parentPrefixSelector = '#VM_' + prefix + '_' + prefix + '_';
        var itemPrefixSelector = prefix + 'Item.';

        //add the ability for a pipe to separate the field to check and the field to highlight (i.e. selectedpress would need something else to highlight, such as the button)
        var parentValidationFields =
            [
                'Client',
                'JobNumber',
                'DueDate'
            ];

        var itemValidationFields =
            [
                'Length',
                'Width',
                'Quantity',
                'QuantityBy'/*,
                'selectedpress',
                'selectedmaterial',
                'PressColorProfileFront',
                'PressColorProfileBack',
                'Material1.ParsedLength',
                'Material1.ParsedWidth'*/
            ];

        //validate parent record
        for (var i = 0; i < parentValidationFields.length; i++) {
            //first, make sure the field actually exists
            var $item = $(parentPrefixSelector + parentValidationFields[i]);
            if ($item.length) {
                ValidateItem($item);
            }
        };


        //validate items
        //foreach item row
        $('.item-row').each(function () {
            var $row = $(this);

            for (var i = 0; i < itemValidationFields.length; i++) {
                var selector = 'input[name="' + itemPrefixSelector + itemValidationFields[i] + '"]';
                var $item = $row.find(selector);
                if ($item.length) {
                    ValidateItem($item);
                }
            }
        });
    }
};