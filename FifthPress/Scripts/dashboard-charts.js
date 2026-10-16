// Global Variables
let rangeStart = '', rangeEnd = '';

let agencyOverviewChart, agencyUtilizationChart, quoteMetricsChart, jobCompletionMetricsChart,
    orgCostChart, topClientsChart, marginChart;

let iAgencyOverviewTable, aCompletedJobSummaryTable, aTopClientsTable, aUtilizationTable;

// Plugins
const averageLinePlugin = {
    id: 'averageLinePlugin',
    afterDraw: (chart, args, options) => {
        const { ctx, chartArea: { top, right, bottom, left }, scales: { y } } = chart;
        if (options && options.yValue != null) {
            ctx.save();
            ctx.strokeStyle = options.color || 'green';
            ctx.lineWidth = options.lineWidth || 2;
            ctx.setLineDash(options.borderDash || [5, 5]);
            ctx.beginPath();
            ctx.moveTo(left, y.getPixelForValue(options.yValue));
            ctx.lineTo(right, y.getPixelForValue(options.yValue));
            ctx.stroke();
            ctx.restore();
        }
        if (options && options.displayInLegend) {
            if (!chart.legend.legendItems.find(item => item.text === 'Average Line')) {
                chart.legend.legendItems.push({
                    text: 'Average Line',
                    fillStyle: options.color || 'green',
                    lineDash: options.borderDash || [5, 5],
                    strokeStyle: options.color || 'green'
                });
            }
        }
    }
};

const horizontalLinePlugin = {
    id: 'horizontalLinePlugin',
    beforeDraw: (chart, args, options) => {
        const { ctx, chartArea: { top, right, bottom, left }, scales: { y } } = chart;

        options.forEach(option => {
            console.log('Drawing line at yValue:', option.yValue);
            if (option && option.yValue != null) {
                ctx.save();
                ctx.strokeStyle = option.color || 'blue';
                ctx.lineWidth = option.lineWidth || 2;
                ctx.beginPath();
                ctx.moveTo(left, y.getPixelForValue(option.yValue));
                ctx.lineTo(right, y.getPixelForValue(option.yValue));
                ctx.stroke();
                ctx.restore();
            }
        });
    }
};


const trendLinePlugin = {
    id: 'trendLinePlugin',
    afterDraw: (chart, args, options) => {
        const { ctx, chartArea: { top, right, bottom, left }, scales: { x, y } } = chart;
        chart.data.datasets.forEach((dataset, index) => {
            const data = dataset.data;

            if (data.length > 1) {
                // Calculate the trendline equation (y = mx + b)
                const n = data.length;
                const sumX = Array.from({ length: n }, (_, i) => i).reduce((a, b) => a + b, 0);
                const sumY = data.reduce((a, b) => a + b, 0);
                const sumXY = data.reduce((acc, point, idx) => acc + idx * point, 0);
                const sumXX = Array.from({ length: n }, (_, i) => i * i).reduce((a, b) => a + b, 0);

                const slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
                const intercept = (sumY - slope * sumX) / n;

                // Determine pixel coordinates for the trendline
                const startX = x.getPixelForValue(0);
                const endX = x.getPixelForValue(n - 1);
                const startY = y.getPixelForValue(slope * 0 + intercept);
                const endY = y.getPixelForValue(slope * (n - 1) + intercept);

                // Set the trendline color to match the dataset's color
                const trendlineColor = dataset.backgroundColor || dataset.borderColor || 'red';

                // Draw the trendline
                ctx.save();
                ctx.strokeStyle = trendlineColor;
                ctx.lineWidth = options.lineWidth || 2;
                ctx.setLineDash(options.borderDash || [5, 5]);
                ctx.beginPath();
                ctx.moveTo(startX, startY);
                ctx.lineTo(endX, endY);
                ctx.stroke();
                ctx.restore();

                // Add the trendline label to the legend if enabled
                if (options && options.displayInLegend) {
                    const legendLabel = dataset.label + ' Trend Line';
                    if (!chart.legend.legendItems.find(item => item.text === legendLabel)) {
                        chart.legend.legendItems.push({
                            text: legendLabel,
                            fillStyle: trendlineColor,
                            lineDash: options.borderDash || [5, 5],
                            strokeStyle: trendlineColor
                        });
                    }
                }
            }
        });
    }
};

const percentageDisplayPlugin = {
    id: 'percentageLabels',
    afterDatasetsDraw: function (chart) {
        const ctx = chart.ctx;

        chart.data.datasets.forEach((dataset, datasetIndex) => {
            const meta = chart.getDatasetMeta(datasetIndex);

            if (!meta.hidden) {
                meta.data.forEach((element, index) => {
                    // Get the value and position
                    const value = dataset.data[index];

                    // Don't draw labels for small segments
                    if (value < 5) return;

                    // Calculate position for text
                    const position = element.getCenterPoint();

                    // Determine if we need to adjust text color based on background
                    ctx.fillStyle = 'black';

                    // Draw the text
                    ctx.textAlign = 'center';
                    ctx.textBaseline = 'middle';
                    ctx.font = '14px Lato';
                    ctx.fillText(`${Math.round(value)}%`, position.x, position.y);
                });
            }
        });
    }
}

Chart.register(averageLinePlugin, horizontalLinePlugin, trendLinePlugin, percentageDisplayPlugin);

// Chart Initialization Functions
function initializeUtilizationTable() {
    if (window.aUtilizationTable) window.aUtilizationTable.destroy();
    window.aUtilizationTable = $('#utilization_table').DataTable();
}
function initializeChart(canvasId, config, chartRef) {
    const canvas = $('#' + canvasId);
    if (!canvas.length) {
        console.error('Canvas element not found:', canvasId);
        return;
    }

    const ctx = canvas.get(0).getContext('2d');
    if (window[chartRef]) window[chartRef].destroy();
    window[chartRef] = new Chart(ctx, config);
}

// Fix for u01 (Print Agency Overview Chart)
function initializePrintAgencyOverviewChart(canvasId, records, targetUtilization) {
    const { dailyRecords, aggregateTotals } = processPrintAgencyData(records);
    const chartConfig = createChartConfig("bar", dailyRecords, 'Utilization', 'rgba(54, 162, 235, 0.5)', 120, 'rgba(54, 162, 235, 0.5)');

    // Add target utilization line
    chartConfig.options.plugins.horizontalLinePlugin = [
        {
            yValue: targetUtilization * 100, // Convert target utilization to percentage
            color: 'rgba(255, 0, 0, 1)', // Red line for target utilization
            lineWidth: 2,
            borderDash: [10, 5], // Dashed line
            displayInLegend: true,
            label: 'Avg. Utz.'
        },
        {
            yValue: 80,
            color: 'red',
            lineWidth: 2,
            displayInLegend: true,
            label: 'Tgt. Utz.'
        }
    ];

    initializeChart(canvasId, chartConfig, 'agencyOverviewChart');
    displayPrintAggregateTotals(aggregateTotals);
    initializeUtilizationTable();
}

function initializeAgencyUtilizationChart(canvasId, records, datasetAverages, targetUtilization) {
    const { labels, datasets } = processAgencyUtilizationData(records);

    const chartConfig = createChartConfigMultipleDatasets('bar', labels, datasets, 120, canvasId);

    // Add horizontal lines for dataset averages and target utilization
    chartConfig.options.plugins.horizontalLinePlugin = [
        {
            yValue: datasetAverages.InternalBillableAverageUtilization * 100,
            color: 'rgba(54, 162, 235, 1)', // Internal Billable Line Color
            lineWidth: 2,
            displayInLegend: true
        },
        {
            yValue: datasetAverages.ExternalBillableAverageUtilization * 100,
            color: 'rgba(75, 192, 192, 1)', // External Billable Line Color
            lineWidth: 2,
            displayInLegend: true
        },
        {
            yValue: datasetAverages.NonBillableAverageUtilization * 100,
            color: 'rgba(255, 159, 64, 1)', // Non-Billable Line Color
            lineWidth: 2,
            displayInLegend: true
        },
        {
            yValue: targetUtilization * 100, // Convert to percentage
            color: 'rgba(255, 0, 0, 1)', // Target Line Color (red)
            lineWidth: 2,
            borderDash: [10, 5], // Dashed line
            displayInLegend: true,
            label: 'Target Utilization'
        }
    ];

    initializeChart(canvasId, chartConfig, 'agencyUtilizationChart');
}

function initializePrintAgencyFullOverviewChart(canvasId, records, datasetAverages, targetUtilization) {
    const { labels, datasets } = processPrintAgencyUtilizationData(records);

    const chartConfig = createChartConfigMultipleDatasets('bar', labels, datasets, 120, canvasId);

    // Add horizontal lines for dataset averages and target utilization
    chartConfig.options.plugins.horizontalLinePlugin = [
        {
            yValue: datasetAverages.InternalBillableAverageUtilization * 100,
            color: 'rgba(54, 162, 235, 1)', // Internal Billable Line Color
            lineWidth: 2,
            displayInLegend: true
        },
        {
            yValue: datasetAverages.ExternalBillableAverageUtilization * 100,
            color: 'rgba(75, 192, 192, 1)', // External Billable Line Color
            lineWidth: 2,
            displayInLegend: true
        },
        {
            yValue: datasetAverages.NonBillableAverageUtilization * 100,
            color: 'rgba(255, 159, 64, 1)', // Non-Billable Line Color
            lineWidth: 2,
            displayInLegend: true
        },
        {
            yValue: targetUtilization * 100, // Convert to percentage
            color: 'rgba(255, 0, 0, 1)', // Target Line Color (red)
            lineWidth: 2,
            borderDash: [10, 5], // Dashed line
            displayInLegend: true,
            label: 'Target Utilization'
        }
    ];

    // Add the percentage labels plugin to the chart config
    // This assumes that createChartConfigMultipleDatasets initializes options.plugins
    if (!chartConfig.plugins) {
        chartConfig.plugins = [];
    }

    // Add the percentage display plugin to the chart's plugins array
    chartConfig.plugins.push(percentageDisplayPlugin);

    initializeChart(canvasId, chartConfig, 'printUtilizationFullChart');
}

function initializeQuoteMetricsChart(canvasId, records) {
    const data = formatDateRecords(records, item => item.Data.PercentConverted);
    const chartConfig = createChartConfig("line", data, 'Conversion Rate', 'rgba(75, 192, 192, 1)', null, 'rgba(75, 192, 192, 0.5)');

    initializeChart(canvasId, chartConfig, 'quoteMetricsChart');
}

function initializeJobCompletionMetricsChart(canvasId, records) {
    // Ensure previous instance of the chart is destroyed
    if (window.jobCompletionMetricsChart) {
        console.log('Destroying previous instance of jobCompletionMetricsChart');
        window.jobCompletionMetricsChart.destroy();
    }

    // Process the data for the chart
    const metrics = processJobCompletionMetrics(records);
    console.log('Processed Metrics for jcm01:', metrics);

    // Configure the chart
    const chartConfig = {
        type: 'line',
        data: {
            labels: records.map(record => record.Date.split('T')[0]), // Use dates as labels
            datasets: [
                {
                    label: 'Not Started Jobs',
                    data: metrics.NotStartedJobs.map(point => point.y),
                    borderColor: 'rgba(255, 99, 132, 1)',
                    backgroundColor: 'rgba(255, 99, 132, 0.2)',
                    borderWidth: 2,
                },
                {
                    label: 'On Time Jobs',
                    data: metrics.OnTimeJobs.map(point => point.y),
                    borderColor: 'rgba(54, 162, 235, 1)',
                    backgroundColor: 'rgba(54, 162, 235, 0.2)',
                    borderWidth: 2,
                },
                {
                    label: 'Past Due Jobs',
                    data: metrics.PastDueJobs.map(point => point.y),
                    borderColor: 'rgba(255, 206, 86, 1)',
                    backgroundColor: 'rgba(255, 206, 86, 0.2)',
                    borderWidth: 2,
                },
            ],
        },
        options: {
            responsive: true,
            scales: {
                x: {
                    type: 'category', // Use a categorical x-axis, not time
                    title: {
                        display: true,
                        text: 'Date',
                    },
                },
                y: {
                    beginAtZero: true,
                    title: {
                        display: true,
                        text: 'Jobs',
                    },
                },
            },
            plugins: {
                legend: {
                    display: true,
                    position: 'top',
                },
                tooltip: {
                    enabled: true,
                },
            },
        },
    };

    // Initialize the chart
    const ctx = document.getElementById(canvasId).getContext('2d');
    window.jobCompletionMetricsChart = new Chart(ctx, chartConfig);
}
// Data Processing Functions
function processPrintAgencyData(records) {
    const aggregateSums = { TotalLateClockIns: 0, TotalEarlyClockOuts: 0, TotalWorkedHours: 0, TotalJobTime: 0, AvgUtilization: 0 };
    let recordCount = 0;

    const dailyRecords = records.map(record => {
        const date = record.Date.split('T')[0];
        const aggregates = record.Aggregates;
        Object.keys(aggregateSums).forEach(key => aggregateSums[key] += aggregates[key] || 0);
        recordCount++;
        return { x: date, y: aggregates.AvgUtilization * 100 };
    });

    const averageUtilization = recordCount ? aggregateSums.AvgUtilization / recordCount : 0;
    return { dailyRecords, aggregateTotals: { ...aggregateSums, AvgUtilization: averageUtilization } };
}

function processPrintAgencyUtilizationData(records) {
    const labels = [];
    const internalBillableData = [];
    const externalBillableData = [];
    const nonBillableData = [];
    const aggregateTotals = {
        TotalInternalBillableHours: 0,
        TotalExternalBillableHours: 0,
        TotalNonBillableHours: 0
    };

    records.forEach(record => {
        const date = record.Date.split('T')[0];
        const aggregates = record.Aggregates;

        labels.push(date);
        internalBillableData.push((aggregates.AvgInternalUtilization || 0) * 100);
        externalBillableData.push((aggregates.AvgExternalUtilization || 0) * 100);
        nonBillableData.push((aggregates.AvgNonBillableUtilization || 0) * 100);

        aggregateTotals.TotalInternalBillableHours += aggregates.TotalInternalBillableJobTime || 0;
        aggregateTotals.TotalExternalBillableHours += aggregates.TotalExternalBillableJobTime || 0;
        aggregateTotals.TotalNonBillableHours += aggregates.TotalNonBillableJobTime || 0;
    });

    const datasets = [
        {
            label: 'Internal Billable Utilization',
            data: internalBillableData,
            backgroundColor: 'rgba(54, 162, 235, 0.5)',
            borderColor: 'rgba(54, 162, 235, 1)',
            borderWidth: 1
        },
        {
            label: 'External Billable Utilization',
            data: externalBillableData,
            backgroundColor: 'rgba(75, 192, 192, 0.5)',
            borderColor: 'rgba(75, 192, 192, 1)',
            borderWidth: 1
        },
        {
            label: 'Non-Billable Utilization',
            data: nonBillableData,
            backgroundColor: 'rgba(255, 159, 64, 0.5)',
            borderColor: 'rgba(255, 159, 64, 1)',
            borderWidth: 1
        }
    ];
    console.log({ labels, datasets, aggregateTotals });
    return { labels, datasets, aggregateTotals };
}

function processAgencyUtilizationData(records) {
    const labels = [];
    const internalBillableData = [];
    const externalBillableData = [];
    const nonBillableData = [];
    const aggregateTotals = {
        TotalInternalBillableHours: 0,
        TotalExternalBillableHours: 0,
        TotalNonBillableHours: 0
    };

    records.forEach(record => {
        const date = record.Date.split('T')[0];
        const aggregates = record.Aggregates;

        labels.push(date);
        internalBillableData.push((aggregates.Utilization_InternalBillable || 0) * 100);
        externalBillableData.push((aggregates.Utilization_ExternalBillable || 0) * 100);
        nonBillableData.push((aggregates.Utilization_NonBillable || 0) * 100);

        aggregateTotals.TotalInternalBillableHours += aggregates.TotalHours_InternalBillable || 0;
        aggregateTotals.TotalExternalBillableHours += aggregates.TotalHours_ExternalBillable || 0;
        aggregateTotals.TotalNonBillableHours += aggregates.TotalHours_NonBillable || 0;
    });

    const datasets = [
        {
            label: 'Internal Billable Utilization',
            data: internalBillableData,
            backgroundColor: 'rgba(54, 162, 235, 0.5)',
            borderColor: 'rgba(54, 162, 235, 1)',
            borderWidth: 1
        },
        {
            label: 'External Billable Utilization',
            data: externalBillableData,
            backgroundColor: 'rgba(75, 192, 192, 0.5)',
            borderColor: 'rgba(75, 192, 192, 1)',
            borderWidth: 1
        },
        {
            label: 'Non-Billable Utilization',
            data: nonBillableData,
            backgroundColor: 'rgba(255, 159, 64, 0.5)',
            borderColor: 'rgba(255, 159, 64, 1)',
            borderWidth: 1
        }
    ];

    return { labels, datasets, aggregateTotals };
}

function formatDateRecords(records, dataAccessor) {
    return records.map(item => ({
        x: item.Date.split('T')[0],
        y: dataAccessor(item)
    }));
}


function processJobCompletionMetrics(records) {
    const metrics = { NotStartedJobs: [], OnTimeJobs: [], PastDueJobs: [] };

    records.forEach((item, index) => {
        metrics.NotStartedJobs.push({ x: index, y: item.Data.NotStartedJobs || 0 });
        metrics.OnTimeJobs.push({ x: index, y: item.Data.OnTimeJobs || 0 });
        metrics.PastDueJobs.push({ x: index, y: item.Data.PastDueJobs || 0 });
    });

    return metrics;
}

// Chart Configurations
function createChartConfig(chartType, data, label, borderColor, yMax, backgroundColor = null) {
    const dataValues = data.map(record => record.y);
    const averageValue = dataValues.reduce((sum, value) => sum + value, 0) / dataValues.length;

    return {
        type: chartType,
        data: {
            labels: data.map(record => record.x),
            datasets: [
                {
                    label,
                    data: dataValues,
                    backgroundColor: backgroundColor || borderColor,
                    borderColor,
                    borderWidth: 2,
                    fill: !!backgroundColor
                }
            ]
        },
        options: {
            responsive: true,
            plugins: {
                averageLinePlugin: {
                    yValue: averageValue,
                    color: 'green',
                    lineWidth: 2,
                    borderDash: [5, 5],
                    displayInLegend: true
                },
                horizontalLinePlugin: {
                    yValue: 80,  // Example value, can be customized
                    color: 'blue',
                    lineWidth: 2,
                    displayInLegend: true
                },
                trendLinePlugin: {
                    color: 'red',
                    lineWidth: 2,
                    borderDash: [5, 5],
                    displayInLegend: true
                }
            },
            scales: { y: { beginAtZero: true, max: yMax } }
        }
    };
}

function createChartConfigMultipleDatasets(chartType, labels, datasets, yMax, canvasId) {
    const isStackedChart = canvasId === 'au01' || canvasId === 'u02' || (canvasId === 'iu01'); // Stacking logic

    return {
        type: chartType,
        data: {
            labels: labels,
            datasets: datasets
        },
        options: {
            responsive: true,
            scales: {
                x: isStackedChart ? { stacked: true } : { stacked: false },
                y: {
                    beginAtZero: true,
                    max: yMax,
                    stacked: isStackedChart
                }
            },
            plugins: {
                trendLinePlugin: {
                    lineWidth: 2,
                    borderDash: [5, 5],
                    displayInLegend: true
                }
            }
        }
    };
}

//NOTE: following function is currently for Job Completion metrics ONLY but should be normalized with the function above (createChartConfigMultipleDatasets)
function createMultiDatasetChartConfig(metrics, yMax) {
    const colors = {
        NotStartedJobs: 'rgba(68, 154, 206, 1)',
        OnTimeJobs: 'rgba(46, 186, 15, 1)',
        PastDueJobs: 'rgba(186, 15, 61, 1)'
    };

    return {
        type: 'line',
        data: {
            datasets: Object.entries(metrics).map(([key, data]) => ({
                label: key.replace(/([A-Z])/g, ' $1'),
                data,
                borderColor: colors[key],
                backgroundColor: colors[key].replace(/1\)$/, '0.5)'),
                borderWidth: 2
            }))
        },
        options: {
            responsive: true,
            scales: { y: { beginAtZero: true, max: yMax } }
        }
    };
}

// Utility Functions
function displayPrintAggregateTotals(aggregateTotals) {
    let $totalsRow = $('.utilization-totals-row');
    $totalsRow.find('.early-clock-outs').html(aggregateTotals.TotalEarlyClockOuts);
    $totalsRow.find('.late-clock-ins').html(aggregateTotals.TotalLateClockIns);
    $totalsRow.find('.hours-worked').html(aggregateTotals.TotalWorkedHours.toFixed(2));
    $totalsRow.find('.job-time').html(aggregateTotals.TotalJobTime.toFixed(2));
}

function displayAgencyAggregateTotals(aggregateTotals) {
    // Implement this function to display aggregate totals for the agency utilization chart
}

function initializeUtilizationTable() {
    if (window.aUtilizationTable) window.aUtilizationTable.destroy();
    window.aUtilizationTable = $('#utilization_table').DataTable();
}

function getRandomColor() {
    return `#${Math.floor(Math.random() * 16777215).toString(16)}`;
}

function toggleUserDisplay() {
    let empName = $(this).parent().attr('data-emp-name');
    let dataset = window.agencyOverviewChart.data.datasets.find(ds => ds.label === empName);

    if (dataset) {
        dataset.hidden = !dataset.hidden;
        window.agencyOverviewChart.update();
    }
}

// Event Bindings
$(function () { })
    .on('click', '.g-display-cb', toggleUserDisplay);

// Chart Selection

/* need to add new cases here if there are new charts */
function Charts(ajaxId, records, userTotals, chartType, datasetAverages, targetUtilization) {
    const canvasId = ajaxId;

    if (ajaxId === 'iu01') {
        console.log('iu01 found');
        switch (chartType) {
            case "Agency":
                console.log('for agency');
                initializeAgencyUtilizationChart(canvasId, records, datasetAverages, targetUtilization);
                break;
            case "Print":
                initializePrintAgencyFullOverviewChart(canvasId, records, datasetAverages, targetUtilization);
                break;
            default:
                console.error('Unknown chart type for iu01:', chartType);
                break;
        }
    } else {
        switch (ajaxId) {
            case 'bd01':
                break;
            case 'au01':
                initializeAgencyUtilizationChart(canvasId, records, datasetAverages, targetUtilization);
                break;
            case 'u01':
                initializePrintAgencyOverviewChart(canvasId, records, targetUtilization);
                break;
            case 'u02':
                initializePrintAgencyFullOverviewChart(canvasId, records, datasetAverages, targetUtilization);
                break;
            case 'jcm01':
                console.log(records);
                initializeJobCompletionMetricsChart(canvasId, records);
                break;
            case 'tc01':
                if ($.fn.DataTable.isDataTable('#top_clients_table')) {
                    $('#top_clients_table').DataTable().destroy();
                    $('#top_clients_table tbody').empty();
                }
                $('#top_clients_table').DataTable();
                break;
            default:
                console.error('Invalid ajaxId:', ajaxId);
                break;
        }
    }
}