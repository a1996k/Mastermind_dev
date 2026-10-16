// Client Cost Splits Management
// Handles dynamic add/remove of client allocations with percentage validation

var ClientCostSplits = (function () {
    var config = {
        entityId: 0,
        entityType: 'Job', // 'Job' or 'Quote'
        baseClientId: 0,
        baseClientName: '',
        totalAmount: 0,
        splits: [],
        onSplitsChanged: null // callback function
    };

    var selectors = {
        container: '#clientCostSplitsSection',
        tableBody: '#clientSplitsBody',
        totalPercent: '#splitTotalPercent',
        totalAmount: '#splitTotalAmount',
        addButton: '#addClientSplitBtn',
        saveButton: '#saveClientSplitsBtn',
        validationMessage: '#splitValidationMessage',
        clientDropdown: '#newClientSelect'
    };

    // Initialize the splits UI
    function init(options) {
        config = $.extend(config, options);

        bindEvents();
        loadExistingSplits();
    }

    // Bind event handlers
    function bindEvents() {
        $(document).on('click', selectors.addButton, showAddClientModal);
        $(document).on('click', selectors.saveButton, saveSplits);
        $(document).on('click', '.remove-client-split', removeClientSplit);
        $(document).on('change', '.split-percentage-input', onPercentageChange);
        $(document).on('blur', '.split-percentage-input', validateAndUpdateTotals);
    }

    // Load existing splits from server
    function loadExistingSplits() {
        if (config.entityId <= 0) {
            // New entity - initialize with base client at 100%
            if (config.baseClientId > 0) {
                config.splits = [{
                    splitId: 0,
                    clientId: config.baseClientId,
                    clientName: config.baseClientName,
                    splitPercentage: 100,
                    isBaseClient: true,
                    calculatedAmount: config.totalAmount
                }];
                renderSplits();
            }
            return;
        }

        var url = config.entityType === 'Quote'
            ? '/Job/GetQuoteClientSplits?quoteId=' + config.entityId
            : '/Job/GetJobClientSplits?jobId=' + config.entityId;

        $.ajax({
            url: url,
            type: 'GET',
            success: function (response) {
                if (response.success && response.splits) {
                    config.splits = response.splits;
                    if (config.splits.length === 0 && config.baseClientId > 0) {
                        // No splits exist yet, initialize with base client
                        config.splits = [{
                            splitId: 0,
                            clientId: config.baseClientId,
                            clientName: config.baseClientName,
                            splitPercentage: 100,
                            isBaseClient: true,
                            calculatedAmount: config.totalAmount
                        }];
                    }
                    renderSplits();
                } else {
                    // API returned error - initialize with base client
                    initializeWithBaseClient();
                }
            },
            error: function () {
                // Failed to load - initialize with base client (table may not exist yet)
                console.warn('Client splits API unavailable - using default base client');
                initializeWithBaseClient();
            }
        });
    }

    // Initialize with just base client when API is unavailable
    function initializeWithBaseClient() {
        if (config.baseClientId > 0) {
            config.splits = [{
                splitId: 0,
                clientId: config.baseClientId,
                clientName: config.baseClientName,
                splitPercentage: 100,
                isBaseClient: true,
                calculatedAmount: config.totalAmount
            }];
            renderSplits();
        }
    }

    // Normalize split object property names (handle PascalCase from server)
    function normalizeSplit(split) {
        return {
            splitId: split.splitId || split.SplitId || 0,
            clientId: split.clientId || split.ClientId || 0,
            clientName: split.clientName || split.ClientName || '',
            splitPercentage: parseFloat(split.splitPercentage || split.SplitPercentage || 0),
            isBaseClient: split.isBaseClient || split.IsBaseClient || false,
            calculatedAmount: parseFloat(split.calculatedAmount || split.CalculatedAmount || 0)
        };
    }

    // Render splits table
    function renderSplits() {
        var $tbody = $(selectors.tableBody);
        $tbody.empty();

        // Normalize all splits first
        config.splits = config.splits.map(normalizeSplit);

        config.splits.forEach(function (split, index) {
            var calculatedAmount = config.totalAmount * (split.splitPercentage / 100);
            var isBaseClient = split.clientId === config.baseClientId || split.isBaseClient;

            var row = '<tr data-client-id="' + split.clientId + '" data-index="' + index + '">' +
                '<td>' +
                    '<span class="client-name">' + escapeHtml(split.clientName) + '</span>' +
                    (isBaseClient ? ' <span class="badge badge-primary">Base</span>' : '') +
                '</td>' +
                '<td>' +
                    '<div class="input-group input-group-sm">' +
                        '<input type="number" class="form-control split-percentage-input" ' +
                            'value="' + split.splitPercentage.toFixed(2) + '" ' +
                            'min="0.01" max="100" step="0.01" ' +
                            'data-client-id="' + split.clientId + '">' +
                        '<div class="input-group-append">' +
                            '<span class="input-group-text">%</span>' +
                        '</div>' +
                    '</div>' +
                '</td>' +
                '<td class="text-right calculated-amount">' + formatCurrency(calculatedAmount) + '</td>' +
                '<td class="text-center">' +
                    (isBaseClient
                        ? '<button type="button" class="btn btn-sm btn-outline-secondary" disabled title="Base client cannot be removed"><i class="fa fa-lock"></i></button>'
                        : '<button type="button" class="btn btn-sm btn-danger remove-client-split" data-client-id="' + split.clientId + '" title="Remove"><i class="fa fa-times"></i></button>') +
                '</td>' +
            '</tr>';

            $tbody.append(row);
        });

        updateTotals();
    }

    // Update totals row
    function updateTotals() {
        var totalPercent = 0;
        var totalAmount = 0;

        config.splits.forEach(function (split) {
            totalPercent += parseFloat(split.splitPercentage) || 0;
            totalAmount += config.totalAmount * (split.splitPercentage / 100);
        });

        $(selectors.totalPercent).text(totalPercent.toFixed(2) + '%');
        $(selectors.totalAmount).text(formatCurrency(totalAmount));

        // Validate total
        var isValid = Math.abs(totalPercent - 100) < 0.01;
        var $totalPercent = $(selectors.totalPercent);
        var $validation = $(selectors.validationMessage);

        if (isValid) {
            $totalPercent.removeClass('text-danger').addClass('text-success');
            $validation.hide();
        } else {
            $totalPercent.removeClass('text-success').addClass('text-danger');
            $validation.text('Percentages must total exactly 100%').show();
        }

        // Update calculated amounts in table
        $(selectors.tableBody + ' tr').each(function () {
            var $row = $(this);
            var clientId = parseInt($row.data('client-id'));
            var split = config.splits.find(function (s) { return s.clientId === clientId; });
            if (split) {
                var amount = config.totalAmount * (split.splitPercentage / 100);
                $row.find('.calculated-amount').text(formatCurrency(amount));
            }
        });

        // Notify callback
        if (typeof config.onSplitsChanged === 'function') {
            config.onSplitsChanged(config.splits, isValid);
        }
    }

    // Handle percentage input change
    function onPercentageChange(e) {
        var $input = $(e.target);
        var clientId = parseInt($input.data('client-id'));
        var newValue = parseFloat($input.val()) || 0;

        // Clamp value
        if (newValue < 0) newValue = 0;
        if (newValue > 100) newValue = 100;

        // Update splits array
        var split = config.splits.find(function (s) { return s.clientId === clientId; });
        if (split) {
            split.splitPercentage = newValue;
        }

        updateTotals();
    }

    // Validate and update totals on blur
    function validateAndUpdateTotals(e) {
        var $input = $(e.target);
        var value = parseFloat($input.val()) || 0;

        if (value < 0.01 && value !== 0) {
            $input.val('0.01');
            value = 0.01;
        }
        if (value > 100) {
            $input.val('100.00');
            value = 100;
        }

        $input.val(value.toFixed(2));

        var clientId = parseInt($input.data('client-id'));
        var split = config.splits.find(function (s) { return s.clientId === clientId; });
        if (split) {
            split.splitPercentage = value;
        }

        updateTotals();
    }

    // Show modal to add a client
    function showAddClientModal() {
        var modalHtml = '<div class="modal fade" id="addClientSplitModal" tabindex="-1">' +
            '<div class="modal-dialog">' +
                '<div class="modal-content">' +
                    '<div class="modal-header">' +
                        '<h5 class="modal-title">Add Client to Cost Split</h5>' +
                        '<button type="button" class="btn-close" data-bs-dismiss="modal" data-dismiss="modal" aria-label="Close"></button>' +
                    '</div>' +
                    '<div class="modal-body">' +
                        '<div class="form-group mb-3">' +
                            '<label for="clientSearchInput">Search Client</label>' +
                            '<input type="text" class="form-control" id="clientSearchInput" placeholder="Type to search clients..." autocomplete="off">' +
                            '<input type="hidden" id="selectedClientId" value="">' +
                            '<div id="clientSearchResults" class="list-group mt-1" style="max-height: 200px; overflow-y: auto; position: absolute; z-index: 1050; width: calc(100% - 2rem);"></div>' +
                        '</div>' +
                        '<div class="form-group">' +
                            '<label for="newSplitPercentage">Allocation Percentage</label>' +
                            '<div class="input-group">' +
                                '<input type="number" class="form-control" id="newSplitPercentage" value="0" min="0.01" max="100" step="0.01">' +
                                '<div class="input-group-append">' +
                                    '<span class="input-group-text">%</span>' +
                                '</div>' +
                            '</div>' +
                            '<small class="text-muted">Remember: Total must equal 100%</small>' +
                        '</div>' +
                    '</div>' +
                    '<div class="modal-footer">' +
                        '<button type="button" class="btn btn-secondary" data-bs-dismiss="modal" data-dismiss="modal">Cancel</button>' +
                        '<button type="button" class="btn btn-primary" id="confirmAddClientSplit">Add Client</button>' +
                    '</div>' +
                '</div>' +
            '</div>' +
        '</div>';

        // Remove existing modal if any
        $('#addClientSplitModal').remove();
        $('body').append(modalHtml);

        var $modal = $('#addClientSplitModal');
        var modalElement = $modal[0];
        var bsModal;
        var searchTimeout;

        // Try Bootstrap 5 first, fall back to Bootstrap 4/jQuery
        if (typeof bootstrap !== 'undefined' && bootstrap.Modal) {
            bsModal = new bootstrap.Modal(modalElement);
            bsModal.show();
        } else {
            $modal.modal('show');
        }

        // Client search autocomplete
        $('#clientSearchInput').on('input', function () {
            var searchTerm = $(this).val();
            clearTimeout(searchTimeout);

            if (searchTerm.length < 1) {
                $('#clientSearchResults').empty().hide();
                return;
            }

            searchTimeout = setTimeout(function () {
                $.ajax({
                    url: '/Job/GetAvailableClientsForSplit',
                    type: 'GET',
                    data: {
                        entityId: config.entityId,
                        entityType: config.entityType,
                        searchTerm: searchTerm
                    },
                    success: function (response) {
                        var $results = $('#clientSearchResults');
                        $results.empty();

                        if (response.success && response.clients && response.clients.length > 0) {
                            response.clients.forEach(function (c) {
                                var id = c.id || c.Id;
                                var name = c.title || c.Title;
                                $results.append(
                                    '<a href="#" class="list-group-item list-group-item-action client-search-item" data-client-id="' + id + '" data-client-name="' + escapeHtml(name) + '">' +
                                        escapeHtml(name) +
                                    '</a>'
                                );
                            });
                            $results.show();
                        } else {
                            $results.append('<div class="list-group-item text-muted">No clients found</div>');
                            $results.show();
                        }
                    }
                });
            }, 300); // Debounce 300ms
        });

        // Handle client selection from autocomplete
        $(document).on('click', '.client-search-item', function (e) {
            e.preventDefault();
            var clientId = $(this).data('client-id');
            var clientName = $(this).data('client-name');

            $('#clientSearchInput').val(clientName);
            $('#selectedClientId').val(clientId);
            $('#clientSearchResults').empty().hide();
        });

        // Hide results when clicking outside
        $(document).on('click', function (e) {
            if (!$(e.target).closest('#clientSearchInput, #clientSearchResults').length) {
                $('#clientSearchResults').hide();
            }
        });

        // Handle add client button
        $('#confirmAddClientSplit').on('click', function () {
            var clientId = parseInt($('#selectedClientId').val());
            var clientName = $('#clientSearchInput').val();
            var percentage = parseFloat($('#newSplitPercentage').val()) || 0;

            if (!clientId) {
                alert('Please select a client from the search results');
                return;
            }

            if (percentage <= 0 || percentage > 100) {
                alert('Percentage must be between 0.01 and 100');
                return;
            }

            addClientSplit(clientId, clientName, percentage);

            // Hide modal
            if (bsModal) {
                bsModal.hide();
            } else {
                $modal.modal('hide');
            }
        });

        // Clean up on close
        modalElement.addEventListener('hidden.bs.modal', function () {
            $modal.remove();
        });
        $modal.on('hidden.bs.modal', function () {
            $(this).remove();
        });
    }

    // Add a new client split
    function addClientSplit(clientId, clientName, percentage) {
        // Check if client already exists
        var existing = config.splits.find(function (s) { return s.clientId === clientId; });
        if (existing) {
            alert('This client is already in the split list');
            return;
        }

        config.splits.push({
            splitId: 0,
            clientId: clientId,
            clientName: clientName,
            splitPercentage: percentage,
            isBaseClient: false,
            calculatedAmount: config.totalAmount * (percentage / 100)
        });

        renderSplits();
    }

    // Remove a client split
    function removeClientSplit(e) {
        var clientId = parseInt($(e.currentTarget).data('client-id'));

        // Don't allow removing base client
        var split = config.splits.find(function (s) { return s.clientId === clientId; });
        if (split && (split.isBaseClient || split.clientId === config.baseClientId)) {
            alert('Cannot remove the base client');
            return;
        }

        config.splits = config.splits.filter(function (s) { return s.clientId !== clientId; });
        renderSplits();
    }

    // Save splits to server
    function saveSplits() {
        var totalPercent = config.splits.reduce(function (sum, s) {
            return sum + (parseFloat(s.splitPercentage) || 0);
        }, 0);

        if (Math.abs(totalPercent - 100) >= 0.01) {
            alert('Percentages must total exactly 100% before saving');
            return;
        }

        var url = config.entityType === 'Quote'
            ? '/Job/SaveQuoteClientSplits'
            : '/Job/SaveJobClientSplits';

        var data = config.entityType === 'Quote'
            ? { quoteId: config.entityId, splits: config.splits }
            : { jobId: config.entityId, splits: config.splits };

        $.ajax({
            url: url,
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(data),
            success: function (response) {
                if (response.success) {
                    toastr.success('Client cost splits saved successfully');
                    loadExistingSplits(); // Reload to get updated IDs
                } else {
                    toastr.error(response.message || 'Failed to save splits');
                }
            },
            error: function () {
                toastr.error('Failed to save client cost splits');
            }
        });
    }

    // Get current splits data
    function getSplits() {
        return config.splits;
    }

    // Check if splits are valid (total = 100%)
    function isValid() {
        var totalPercent = config.splits.reduce(function (sum, s) {
            return sum + (parseFloat(s.splitPercentage) || 0);
        }, 0);
        return Math.abs(totalPercent - 100) < 0.01;
    }

    // Update base client (when job/quote client changes)
    function updateBaseClient(newClientId, newClientName) {
        var oldBaseClient = config.splits.find(function (s) { return s.isBaseClient; });

        if (oldBaseClient) {
            // Update existing base client entry
            oldBaseClient.clientId = newClientId;
            oldBaseClient.clientName = newClientName;
        } else {
            // Add new base client
            config.splits.unshift({
                splitId: 0,
                clientId: newClientId,
                clientName: newClientName,
                splitPercentage: 100,
                isBaseClient: true,
                calculatedAmount: config.totalAmount
            });
        }

        config.baseClientId = newClientId;
        config.baseClientName = newClientName;
        renderSplits();
    }

    // Update total amount (when job/quote cost changes)
    function updateTotalAmount(newAmount) {
        config.totalAmount = newAmount;
        updateTotals();
    }

    // Helper: Format currency
    function formatCurrency(value) {
        return '$' + value.toFixed(2).replace(/\d(?=(\d{3})+\.)/g, '$&,');
    }

    // Helper: Escape HTML
    function escapeHtml(text) {
        if (!text) return '';
        var div = document.createElement('div');
        div.appendChild(document.createTextNode(text));
        return div.innerHTML;
    }

    // Public API
    return {
        init: init,
        getSplits: getSplits,
        isValid: isValid,
        saveSplits: saveSplits,
        updateBaseClient: updateBaseClient,
        updateTotalAmount: updateTotalAmount,
        addClientSplit: addClientSplit,
        loadExistingSplits: loadExistingSplits
    };
})();
