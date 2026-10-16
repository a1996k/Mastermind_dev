console.log('billingreports.js file loaded at top level');

function initializeTimeAndMaterialTable() {
    console.log('initializeTimeAndMaterialTable called');

    // Check if table exists
    const $table = $('#timeAndMaterialTable');
    console.log('Table found:', $table.length > 0);

    if ($table.length === 0) {
        console.error('Table #timeAndMaterialTable not found');
        return;
    }

    // Destroy existing DataTable if it exists
    if ($.fn.DataTable.isDataTable('#timeAndMaterialTable')) {
        console.log('Destroying existing DataTable');
        $('#timeAndMaterialTable').DataTable().destroy();
    }

    console.log('Initializing DataTable...');

    const now = new Date();
    const yyyy = now.getFullYear();
    const mm = String(now.getMonth() + 1).padStart(2, '0');
    const dd = String(now.getDate()).padStart(2, '0');
    const filename = `${yyyy}-${mm}-${dd}_event_client_time_material_costs`;

    try {
        const table = $('#timeAndMaterialTable').DataTable({
        dom: 'Bfrtip',
        buttons: [
            {
                extend: 'csvHtml5',
                text: '<i class="fas fa-file-csv"></i> Export CSV',
                className: 'btn btn-primary btn-sm',
                filename: filename,
                title: null,
                exportOptions: { columns: ':visible' }
            }
        ],
        scrollX: true,
        paging: false,
        searching: true,
        columnDefs: [
            { targets: [4, 7], type: 'num' },
            { 
                targets: [5, 6, 8, 9, 10, 11, 12],
                type: 'num',
                render: function(data, type, row) {
                    if (type === 'sort' || type === 'type') {
                        return parseFloat(data.replace(/[$,]/g, '')) || 0;
                    }
                    return data;
                }
            },
            { 
                targets: [13],
                type: 'num',
                render: function(data, type, row) {
                    if (type === 'sort' || type === 'type') {
                        // Strip HTML tags and handle parentheses for negative values
                        var cleanData = data.replace(/<[^>]*>/g, '').replace(/[\$,]/g, '');
                        if (cleanData.includes('(') && cleanData.includes(')')) {
                            cleanData = '-' + cleanData.replace(/[()]/g, '');
                        }
                        return parseFloat(cleanData) || 0;
                    }
                    return data;
                }
            }
        ],
        footerCallback: function (row, data, start, end, display) {
            var api = this.api();
            var intVal = function (i) {
                if (typeof i === 'string') {
                    // Handle parentheses for negative values
                    var cleanStr = i.replace(/[\$,]/g, '');
                    if (cleanStr.includes('(') && cleanStr.includes(')')) {
                        cleanStr = '-' + cleanStr.replace(/[()]/g, '');
                    }
                    return parseFloat(cleanStr) || 0;
                }
                return typeof i === 'number' ? i : 0;
            };
            
            var columns = [5, 6, 8, 9, 10, 11, 12, 13];
            columns.forEach(function(colIndex) {
                var total = api
                    .column(colIndex, { page: 'current' })
                    .data()
                    .reduce(function (a, b) {
                        // Strip HTML tags for columns that contain them (Total Cost, Total Price, Revenue)
                        var aVal = typeof a === 'string' ? a.replace(/<[^>]*>/g, '') : a;
                        var bVal = typeof b === 'string' ? b.replace(/<[^>]*>/g, '') : b;
                        return intVal(aVal) + intVal(bVal);
                    }, 0);
                
                $(api.column(colIndex).footer()).html('$' + total.toLocaleString('en-US', {
                    minimumFractionDigits: 2,
                    maximumFractionDigits: 2
                }));
            });
        }
        });
        
        console.log('DataTable initialized successfully');
        return table;
        
    } catch (error) {
        console.error('Error initializing DataTable:', error);
        console.error('Stack trace:', error.stack);
    }
}

function initializeAllClientsTimeAndMaterialTable() {
    console.log('initializeAllClientsTimeAndMaterialTable called');

    // Check if table exists
    const $table = $('#allClientsTimeAndMaterialTable');
    console.log('Table found:', $table.length > 0);

    if ($table.length === 0) {
        console.error('Table #allClientsTimeAndMaterialTable not found');
        return;
    }

    // Destroy existing DataTable if it exists
    if ($.fn.DataTable.isDataTable('#allClientsTimeAndMaterialTable')) {
        console.log('Destroying existing DataTable');
        $('#allClientsTimeAndMaterialTable').DataTable().destroy();
    }

    console.log('Initializing DataTable...');

    const now = new Date();
    const yyyy = now.getFullYear();
    const mm = String(now.getMonth() + 1).padStart(2, '0');
    const dd = String(now.getDate()).padStart(2, '0');
    const filename = `${yyyy}-${mm}-${dd}_all_clients_time_material_costs`;

    try {
        const table = $('#allClientsTimeAndMaterialTable').DataTable({
        dom: 'Bfrtip',
        buttons: [
            {
                extend: 'csvHtml5',
                text: '<i class="fas fa-file-csv"></i> Export CSV',
                className: 'btn btn-primary btn-sm',
                filename: filename,
                title: null,
                exportOptions: { columns: ':visible' }
            }
        ],
        scrollX: true,
        paging: false,
        searching: true,
        columnDefs: [
            { targets: [4, 7], type: 'num' },
            {
                targets: [5, 6, 8, 9, 10, 11, 12],
                type: 'num',
                render: function(data, type, row) {
                    if (type === 'sort' || type === 'type') {
                        return parseFloat(data.replace(/[$,]/g, '')) || 0;
                    }
                    return data;
                }
            },
            {
                targets: [13],
                type: 'num',
                render: function(data, type, row) {
                    if (type === 'sort' || type === 'type') {
                        // Strip HTML tags and handle parentheses for negative values
                        var cleanData = data.replace(/<[^>]*>/g, '').replace(/[\$,]/g, '');
                        if (cleanData.includes('(') && cleanData.includes(')')) {
                            cleanData = '-' + cleanData.replace(/[()]/g, '');
                        }
                        return parseFloat(cleanData) || 0;
                    }
                    return data;
                }
            }
        ],
        footerCallback: function (row, data, start, end, display) {
            var api = this.api();
            var intVal = function (i) {
                if (typeof i === 'string') {
                    // Handle parentheses for negative values
                    var cleanStr = i.replace(/[\$,]/g, '');
                    if (cleanStr.includes('(') && cleanStr.includes(')')) {
                        cleanStr = '-' + cleanStr.replace(/[()]/g, '');
                    }
                    return parseFloat(cleanStr) || 0;
                }
                return typeof i === 'number' ? i : 0;
            };

            var columns = [5, 6, 8, 9, 10, 11, 12, 13];
            columns.forEach(function(colIndex) {
                var total = api
                    .column(colIndex, { page: 'current' })
                    .data()
                    .reduce(function (a, b) {
                        // Strip HTML tags for columns that contain them (Total Cost, Total Price, Revenue)
                        var aVal = typeof a === 'string' ? a.replace(/<[^>]*>/g, '') : a;
                        var bVal = typeof b === 'string' ? b.replace(/<[^>]*>/g, '') : b;
                        return intVal(aVal) + intVal(bVal);
                    }, 0);

                $(api.column(colIndex).footer()).html('$' + total.toLocaleString('en-US', {
                    minimumFractionDigits: 2,
                    maximumFractionDigits: 2
                }));
            });
        }
        });

        console.log('DataTable initialized successfully');
        return table;

    } catch (error) {
        console.error('Error initializing DataTable:', error);
        console.error('Stack trace:', error.stack);
    }
}

function LoadTimeMaterialReport() {
    // Activate the Time Materials tab
    $('.tab-pane').removeClass('show active');
    $('#timeMaterialReportDisplay').addClass('show active');
    
    $.get(ProcessUrl('Billing/EventClientTimeAndMaterialCosts?clients=-1'))
        .done(function (data) {
            try {
                const $target = $('#timeMaterialReportDisplay');
                
                if ($target.length === 0) {
                    console.error('Target element #timeMaterialReportDisplay not found');
                    return;
                }
                
                // Clear existing content first
                $target.empty();
                
                // Insert new content safely
                $target.html(data);
                
                // Initialize DataTable after content is loaded
                console.log('About to initialize DataTable in 500ms...');
                setTimeout(function() {
                    console.log('Timeout fired, calling initializeTimeAndMaterialTable');
                    initializeTimeAndMaterialTable();
                }, 500);
                
                console.log('Report loaded successfully');
            } catch (ex) {
                console.error('Error inserting report HTML:', ex);
                $('#timeMaterialReportDisplay').html('<div class="alert alert-danger">Error displaying report: ' + ex.message + '</div>');
            }
        })
        .fail(function(xhr, status, error) {
            console.error('Failed to load report:', error);
            $('#timeMaterialReportDisplay').html('<div class="alert alert-danger">Failed to load report: ' + error + '</div>');
        });
};

function UpdateTimeMaterialReport() {
    //grab all checked client IDs
    const selectedIds = $('.Events-msl-dropdown-container .msl-dropdown-list')
        .find('input[type="checkbox"]:checked')
        .map((_, cb) => cb.value)
        .get();

    const query = `?clients=${encodeURIComponent(selectedIds.join(','))}`;
    const url = ProcessUrl('Billing/EventClientTimeAndMaterialCosts' + query);

    $.get(url)
        .done(function (data) {
            try {
                const $target = $('#timeMaterialReportDisplay');

                if ($target.length === 0) {
                    console.error('Target element #timeMaterialReportDisplay not found');
                    return;
                }

                // Clear existing content first
                $target.empty();

                // Insert new content safely
                $target.html(data);

                // Initialize DataTable after content is loaded
                console.log('About to initialize DataTable in 500ms...');
                setTimeout(function() {
                    console.log('Timeout fired, calling initializeTimeAndMaterialTable');
                    initializeTimeAndMaterialTable();
                }, 500);

                console.log('Report updated successfully');
            } catch (ex) {
                console.error('Error updating report HTML:', ex);
                $('#timeMaterialReportDisplay').html('<div class="alert alert-danger">Error updating report: ' + ex.message + '</div>');
            }
        })
        .fail(function (xhr, status, error) {
            console.error(
                `Failed to load report (status ${xhr.status}):\n`,
                xhr.responseText || error
            );
        });
}

function LoadAllClientsTimeMaterialReport() {
    // Activate the All Clients Time Material tab
    $('.tab-pane').removeClass('show active');
    $('#allClientsTimeMaterialReportDisplay').addClass('show active');

    $.get(ProcessUrl('Billing/AllClientTimeAndMaterialCosts?clients=-1'))
        .done(function (data) {
            try {
                const $target = $('#allClientsTimeMaterialReportDisplay');

                if ($target.length === 0) {
                    console.error('Target element #allClientsTimeMaterialReportDisplay not found');
                    return;
                }

                // Clear existing content first
                $target.empty();

                // Insert new content safely
                $target.html(data);

                // Initialize DataTable after content is loaded
                console.log('About to initialize DataTable in 500ms...');
                setTimeout(function() {
                    console.log('Timeout fired, calling initializeAllClientsTimeAndMaterialTable');
                    initializeAllClientsTimeAndMaterialTable();
                }, 500);

                console.log('All Clients Report loaded successfully');
            } catch (ex) {
                console.error('Error inserting report HTML:', ex);
                $('#allClientsTimeMaterialReportDisplay').html('<div class="alert alert-danger">Error displaying report: ' + ex.message + '</div>');
            }
        })
        .fail(function(xhr, status, error) {
            console.error('Failed to load all clients report:', error);
            $('#allClientsTimeMaterialReportDisplay').html('<div class="alert alert-danger">Failed to load report: ' + error + '</div>');
        });
}

function UpdateAllClientsTimeMaterialReport() {
    //grab all checked client IDs
    const selectedIds = $('.AllClients-msl-dropdown-container .msl-dropdown-list')
        .find('input[type="checkbox"]:checked')
        .map((_, cb) => cb.value)
        .get();

    const query = `?clients=${encodeURIComponent(selectedIds.join(','))}`;
    const url = ProcessUrl('Billing/AllClientTimeAndMaterialCosts' + query);

    $.get(url)
        .done(function (data) {
            try {
                const $target = $('#allClientsTimeMaterialReportDisplay');

                if ($target.length === 0) {
                    console.error('Target element #allClientsTimeMaterialReportDisplay not found');
                    return;
                }

                // Clear existing content first
                $target.empty();

                // Insert new content safely
                $target.html(data);

                // Initialize DataTable after content is loaded
                console.log('About to initialize DataTable in 500ms...');
                setTimeout(function() {
                    console.log('Timeout fired, calling initializeAllClientsTimeAndMaterialTable');
                    initializeAllClientsTimeAndMaterialTable();
                }, 500);

                console.log('All Clients Report updated successfully');
            } catch (ex) {
                console.error('Error updating all clients report HTML:', ex);
                $('#allClientsTimeMaterialReportDisplay').html('<div class="alert alert-danger">Error updating report: ' + ex.message + '</div>');
            }
        })
        .fail(function (xhr, status, error) {
            console.error(
                `Failed to load all clients report (status ${xhr.status}):\n`,
                xhr.responseText || error
            );
        });
}

function LoadTournamentAccuracyReport() {
    // Activate the Tournament Accuracy tab
    $('.tab-pane').removeClass('show active');
    $('#tournamentAccuracyReportDisplay').addClass('show active');

    $.get(ProcessUrl('Billing/TournamentServiceAccuracyPartial'))
        .done(function (data) {
            try {
                const $target = $('#tournamentAccuracyReportDisplay');

                if ($target.length === 0) {
                    console.error('Target element #tournamentAccuracyReportDisplay not found');
                    return;
                }

                // Clear existing content first
                $target.empty();

                // Insert new content safely
                $target.html(data);

                console.log('Tournament Accuracy Report loaded successfully');
            } catch (ex) {
                console.error('Error inserting tournament accuracy report HTML:', ex);
                $('#tournamentAccuracyReportDisplay').html('<div class="alert alert-danger">Error displaying report: ' + ex.message + '</div>');
            }
        })
        .fail(function(xhr, status, error) {
            console.error('Failed to load tournament accuracy report:', error);
            $('#tournamentAccuracyReportDisplay').html('<div class="alert alert-danger">Failed to load report: ' + error + '</div>');
        });
}

function LoadCostSheetRollup() {
    // Activate the Cost Sheet Rollup tab
    $('.tab-pane').removeClass('show active');
    $('#costSheetRollupDisplay').addClass('show active');

    $.get(ProcessUrl('Billing/EventCostSheetRollup?clients=0'))
        .done(function (data) {
            try {
                const $target = $('#costSheetRollupDisplay');

                if ($target.length === 0) {
                    console.error('Target element #costSheetRollupDisplay not found');
                    return;
                }

                $target.empty();
                $target.html(data);

                console.log('Cost Sheet Rollup loaded successfully');
            } catch (ex) {
                console.error('Error inserting cost sheet rollup HTML:', ex);
                $('#costSheetRollupDisplay').html('<div class="alert alert-danger">Error displaying report: ' + ex.message + '</div>');
            }
        })
        .fail(function (xhr, status, error) {
            console.error('Failed to load cost sheet rollup:', error);
            $('#costSheetRollupDisplay').html('<div class="alert alert-danger">Failed to load report: ' + error + '</div>');
        });
}

function UpdateCostSheetRollup() {
    const selectedIds = $('.CostSheetEvents-msl-dropdown-container .msl-dropdown-list')
        .find('input[type="checkbox"]:checked')
        .map((_, cb) => cb.value)
        .get();

    const query = `?clients=${encodeURIComponent(selectedIds.join(','))}`;
    const url = ProcessUrl('Billing/EventCostSheetRollup' + query);

    $('#costSheetRollupDisplay').html('<div class="text-center p-5"><div class="spinner-border text-primary" role="status"><span class="sr-only">Loading...</span></div><p class="mt-2">Loading cost sheet data...</p></div>');

    $.get(url)
        .done(function (data) {
            try {
                const $target = $('#costSheetRollupDisplay');

                if ($target.length === 0) {
                    console.error('Target element #costSheetRollupDisplay not found');
                    return;
                }

                $target.empty();
                $target.html(data);

                console.log('Cost Sheet Rollup updated successfully');
            } catch (ex) {
                console.error('Error updating cost sheet rollup HTML:', ex);
                $('#costSheetRollupDisplay').html('<div class="alert alert-danger">Error updating report: ' + ex.message + '</div>');
            }
        })
        .fail(function (xhr, status, error) {
            console.error(
                `Failed to load cost sheet rollup (status ${xhr.status}):\n`,
                xhr.responseText || error
            );
            $('#costSheetRollupDisplay').html('<div class="alert alert-danger">Failed to load report. The request may have timed out — try selecting fewer clients.</div>');
        });
}

/* HIDDEN - All Clients version of Cost Sheet Detail (not the Time & Material report)
   Backend code is ready in BillingController.AllClientsCostSheetRollup() and AllClientsCostSheetRollup.cshtml
   Uncomment the tab in ReportsIndex.cshtml and these functions to enable

function LoadAllClientsCostSheetRollup() {
    // Activate the All Clients Cost Sheet Rollup tab
    $('.tab-pane').removeClass('show active');
    $('#allClientsCostSheetRollupDisplay').addClass('show active');

    $.get(ProcessUrl('Billing/AllClientsCostSheetRollup?clients=0'))
        .done(function (data) {
            try {
                const $target = $('#allClientsCostSheetRollupDisplay');

                if ($target.length === 0) {
                    console.error('Target element #allClientsCostSheetRollupDisplay not found');
                    return;
                }

                $target.empty();
                $target.html(data);

                console.log('All Clients Cost Sheet Rollup loaded successfully');
            } catch (ex) {
                console.error('Error inserting all clients cost sheet rollup HTML:', ex);
                $('#allClientsCostSheetRollupDisplay').html('<div class="alert alert-danger">Error displaying report: ' + ex.message + '</div>');
            }
        })
        .fail(function (xhr, status, error) {
            console.error('Failed to load all clients cost sheet rollup:', error);
            $('#allClientsCostSheetRollupDisplay').html('<div class="alert alert-danger">Failed to load report: ' + error + '</div>');
        });
}

function UpdateAllClientsCostSheetRollup() {
    const selectedIds = $('.CostSheetAllClients-msl-dropdown-container .msl-dropdown-list')
        .find('input[type="checkbox"]:checked')
        .map((_, cb) => cb.value)
        .get();

    const query = `?clients=${encodeURIComponent(selectedIds.join(','))}`;
    const url = ProcessUrl('Billing/AllClientsCostSheetRollup' + query);

    $('#allClientsCostSheetRollupDisplay').html('<div class="text-center p-5"><div class="spinner-border text-primary" role="status"><span class="sr-only">Loading...</span></div><p class="mt-2">Loading cost sheet data...</p></div>');

    $.get(url)
        .done(function (data) {
            try {
                const $target = $('#allClientsCostSheetRollupDisplay');

                if ($target.length === 0) {
                    console.error('Target element #allClientsCostSheetRollupDisplay not found');
                    return;
                }

                $target.empty();
                $target.html(data);

                console.log('All Clients Cost Sheet Rollup updated successfully');
            } catch (ex) {
                console.error('Error updating all clients cost sheet rollup HTML:', ex);
                $('#allClientsCostSheetRollupDisplay').html('<div class="alert alert-danger">Error updating report: ' + ex.message + '</div>');
            }
        })
        .fail(function (xhr, status, error) {
            console.error(
                `Failed to load all clients cost sheet rollup (status ${xhr.status}):\n`,
                xhr.responseText || error
            );
            $('#allClientsCostSheetRollupDisplay').html('<div class="alert alert-danger">Failed to load report. The request may have timed out — try selecting fewer clients.</div>');
        });
}
*/

$(function () {
    console.log('billingreports.js loaded successfully');
})
    .on('click', '#time_material_report_tab', function() {
        console.log('time_material_report_tab clicked');
        LoadTimeMaterialReport();
    })
    .on('click', '#allclients_time_material_report_tab', function() {
        console.log('allclients_time_material_report_tab clicked');
        LoadAllClientsTimeMaterialReport();
    })
    .on('click', '#tournament_accuracy_report_tab', function() {
        console.log('tournament_accuracy_report_tab clicked');
        LoadTournamentAccuracyReport();
    })
    .on('click', '#costsheet_rollup_tab', function() {
        console.log('costsheet_rollup_tab clicked');
        LoadCostSheetRollup();
    })
    /* HIDDEN - All Clients Cost Sheet Detail handlers
    .on('click', '#all_clients_costsheet_rollup_tab', function() {
        console.log('all_clients_costsheet_rollup_tab clicked');
        LoadAllClientsCostSheetRollup();
    })
    */
    .on('click', '.update-time-material-report', function() {
        console.log('update-time-material-report clicked');
        UpdateTimeMaterialReport();
    })
    .on('click', '.update-allclients-time-material-report', function() {
        console.log('update-allclients-time-material-report clicked');
        UpdateAllClientsTimeMaterialReport();
    })
    .on('click', '.update-costsheet-rollup', function() {
        console.log('update-costsheet-rollup clicked');
        UpdateCostSheetRollup();
    })
    /* HIDDEN - All Clients Cost Sheet Detail handlers
    .on('click', '.update-allclients-costsheet-rollup', function() {
        console.log('update-allclients-costsheet-rollup clicked');
        UpdateAllClientsCostSheetRollup();
    })
    */;