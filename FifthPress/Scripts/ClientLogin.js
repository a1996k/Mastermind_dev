// ClientLogin.js - Client Login Management
// Handles client login creation and edit forms with dynamic email fields and Select2 client selection

var ClientLoginForm = (function () {
    let emailCounter = 0;
    let isEditMode = false;
    let clientLoginId = null;

    // Initialize the form
    function init() {
        // Check if this is edit mode
        isEditMode = $('#clientLoginForm').data('mode') === 'edit';
        clientLoginId = $('#clientLoginForm').data('id');

        initializeClientSelect();

        // Only add empty email entry if no emails exist (create mode)
        if ($('.email-entry').length === 0) {
            addEmailEntry();
        }

        bindEvents();
    }

    // Initialize Select2 for client dropdown with AJAX search
    function initializeClientSelect() {
        $('#client-select').select2({
            ajax: {
                url: ProcessUrl('Tournament/GetClients'), // Reusing existing endpoint
                dataType: 'json',
                delay: 250,
                data: function (params) {
                    return {
                        search: params.term || ''
                    };
                },
                processResults: function (data) {
                    return {
                        results: data.map(c => ({ id: c.Id, text: c.Title }))
                    };
                },
                cache: true
            },
            placeholder: '-- Select Clients --',
            allowClear: true,
            width: '100%',
            minimumInputLength: 3,
            language: {
                inputTooShort: function (args) {
                    var remainingChars = args.minimum - args.input.length;
                    return 'Please enter ' + remainingChars + ' or more characters';
                },
                searching: function () {
                    return "Searching...";
                },
                noResults: function () {
                    return "No clients found";
                }
            }
        });
    }

    // Add a new email entry to the form
    function addEmailEntry(email = '', isPrimary = false) {
        const emailId = `email-${emailCounter++}`;
        const emailHtml = `
            <div class="email-entry mb-2" data-email-id="${emailId}">
                <div class="input-group">
                    <input type="email" class="form-control email-input" name="emails" placeholder="email@example.com" value="${email}" required>
                    <div class="input-group-text">
                        <input class="form-check-input mt-0 primary-checkbox" type="checkbox" name="isPrimary" ${isPrimary ? 'checked' : ''}>
                        <label class="form-check-label ms-1">Primary</label>
                    </div>
                    <button type="button" class="btn btn-danger btn-sm remove-email-btn">Remove</button>
                </div>
            </div>
        `;
        $('#emails-container').append(emailHtml);
    }

    // Remove an email entry from the form
    function removeEmailEntry(button) {
        const emailEntries = $('.email-entry');
        if (emailEntries.length > 1) {
            $(button).closest('.email-entry').remove();
        } else {
            alert('At least one email is required');
        }
    }

    // Ensure only one primary checkbox is selected at a time
    function handlePrimaryCheckbox(checkbox) {
        if ($(checkbox).is(':checked')) {
            $('.primary-checkbox').not(checkbox).prop('checked', false);
        }
    }

    // Bind all event handlers
    function bindEvents() {
        // Add email button
        $('#add-email-btn').on('click', function () {
            addEmailEntry();
        });

        // Remove email button (delegated event)
        $(document).on('click', '.remove-email-btn', function () {
            removeEmailEntry(this);
        });

        // Primary checkbox change (delegated event)
        $(document).on('change', '.primary-checkbox', function () {
            handlePrimaryCheckbox(this);
        });

        // Form submission
        $('#clientLoginForm').on('submit', function (e) {
            e.preventDefault();
            submitForm();
        });
    }

    // Submit the form via AJAX
    function submitForm() {
        // Gather form data
        const formData = {
            name: $('#login-name').val(),
            emails: [],
            isPrimary: [],
            clientIds: $('#client-select').val()
        };

        // Add id if in edit mode
        if (isEditMode && clientLoginId) {
            formData.id = clientLoginId;
        }

        // Collect emails and their isPrimary status
        $('.email-entry').each(function () {
            const email = $(this).find('.email-input').val();
            const isPrimary = $(this).find('.primary-checkbox').is(':checked');
            formData.emails.push(email);
            formData.isPrimary.push(isPrimary);
        });

        // Add anti-forgery token
        formData.__RequestVerificationToken = $('input[name="__RequestVerificationToken"]').val();

        // Hide previous messages
        $('#error-message').hide();
        $('#success-message').hide();

        // Disable submit button
        const buttonText = isEditMode ? 'Updating...' : 'Creating...';
        $('#submit-btn').prop('disabled', true).text(buttonText);

        // Determine URL based on mode
        const url = isEditMode ? ProcessUrl('ClientLogin/Edit') : ProcessUrl('ClientLogin/Create');

        // Submit via AJAX
        $.ajax({
            url: url,
            type: 'POST',
            data: formData,
            traditional: true, // Important for arrays
            success: function (response) {
                handleSubmitSuccess(response);
            },
            error: function (xhr, status, error) {
                handleSubmitError(error);
            }
        });
    }

    // Handle successful form submission
    function handleSubmitSuccess(response) {
        if (response.success) {
            $('#success-message').text(response.message).show();

            if (!isEditMode) {
                resetForm();
            }

            // Redirect after 2 seconds
            setTimeout(function () {
                window.location.href = ProcessUrl('ClientLogin/Index');
            }, 2000);
        } else {
            $('#error-message').text(response.message).show();
            const buttonText = isEditMode ? 'Update Client Login' : 'Create Client Login';
            $('#submit-btn').prop('disabled', false).text(buttonText);
        }
    }

    // Handle form submission error
    function handleSubmitError(error) {
        $('#error-message').text('An error occurred: ' + error).show();
        const buttonText = isEditMode ? 'Update Client Login' : 'Create Client Login';
        $('#submit-btn').prop('disabled', false).text(buttonText);
    }

    // Reset the form to initial state
    function resetForm() {
        $('#clientLoginForm')[0].reset();
        $('#emails-container').empty();
        emailCounter = 0;
        addEmailEntry();
        $('#client-select').val(null).trigger('change');
    }

    // Public API
    return {
        init: init,
        addEmailEntry: addEmailEntry
    };
})();

// Initialize on document ready
$(document).ready(function () {
    ClientLoginForm.init();
});
