class Spreadsheet {
    constructor() {
        this.data = {};
        this.selectedCell = null;
        this.selectedCells = []; // For multi-selection
        this.clipboard = null;
        this.isSelecting = false;
        this.selectionStart = null;
        this.selectionEnd = null;
        this.justFinishedDragging = false;
        this.dragMoved = false;
        this.year = new Date().getFullYear();
        this.columnNames = [
            "Alice Johnson",
            "Bob Smith",
            "Carol Williams",
            "David Brown",
            "Emma Davis",
            "Frank Miller",
            "Grace Wilson",
            "Henry Moore",
            "Ivy Taylor",
            "Jack Anderson",
            "Kate Thomas",
            "Liam Jackson",
        ]; // Default names - will be replaced by database data

        // Tournament scheduling data
        this.tournaments = [];
        this.nextTournamentId = 1;
        this.filteredTournamentIds = new Set(); // Empty set means show all tournaments
        this.allScheduleRows = []; // Store complete schedule for filtering

        // Assigned users filter
        this.showOnlyAssignedUsers = false; // Default: show all users
        this.assignedUserColumns = new Set(); // Stores column indices of assigned users

        // Virtual scrolling configuration
        this.virtualScrolling = {
            enabled: true,
            rowHeight: 32,           // Height of each data row in pixels
            headerHeight: 35,        // Height of header row
            bufferRows: 15,          // Extra rows to render above/below viewport
            renderedRange: { start: -1, end: -1 }, // Currently rendered row range
            scrollRAF: null,         // requestAnimationFrame handle for scroll
            isRendering: false       // Flag to prevent concurrent renders
        };

        this.generateScheduleRows();
        this.init();
        this.bindEvents();
        const hasStoredData = this.loadFromStorage();
        if (!hasStoredData) {
            // If no localStorage data, initialize from server-provided data
            this.initializeFromServerData();
        } else {
            // If we have localStorage data, sort it now since EventScheduleAPI won't run
            this.sortColumnsByAssignmentCount();
        }
    }

    // Helper to check if a tournament has any activity (including phase dates) in a given year
    isTournamentInYear(tournament, year) {
        const getYear = (date) => {
            if (!date) return null;
            const d = date instanceof Date ? date : new Date(date);
            return d.getFullYear();
        };

        return (
            getYear(tournament.startDate) === year ||
            getYear(tournament.endDate) === year ||
            getYear(tournament.setupStart) === year ||
            getYear(tournament.setupEnd) === year ||
            getYear(tournament.advancedStart) === year ||
            getYear(tournament.advancedEnd) === year ||
            getYear(tournament.teardownStart) === year ||
            getYear(tournament.teardownEnd) === year
        );
    }

    generateScheduleRows() {
        this.scheduleRows = [];
        this.daysInYear = this.isLeapYear(this.year) ? 366 : 365;

        // Create a map to track ALL tournament activities by date (supports overlapping tournaments)
        const tournamentActivities = new Map(); // dateKey -> array of activities

        // Filter tournaments based on current filter and sort by start date
        let tournamentsToProcess = [...this.tournaments];

        // Apply tournament filter if any specific tournaments are selected
        if (this.filteredTournamentIds.size > 0) {
            tournamentsToProcess = tournamentsToProcess.filter(tournament =>
                this.filteredTournamentIds.has(tournament.id)
            );
        }

        const sortedTournaments = tournamentsToProcess.sort(
            (a, b) => a.startDate - b.startDate
        );

        // Helper to add activity to map (supports multiple tournaments per day)
        const addActivity = (dateKey, activity) => {
            if (!tournamentActivities.has(dateKey)) {
                tournamentActivities.set(dateKey, []);
            }
            tournamentActivities.get(dateKey).push(activity);
        };

        // Helper to get date value or calculate default
        const getDateValue = (value, fallbackFn) => {
            if (value) {
                return value instanceof Date ? value : new Date(value);
            }
            return fallbackFn();
        };

        for (const tournament of sortedTournaments) {
            // Calculate phase dates once per tournament
            const setupStart = getDateValue(tournament.setupStart, () => {
                const d = new Date(tournament.startDate);
                d.setDate(d.getDate() - 21);
                return d;
            });

            const setupEnd = getDateValue(tournament.setupEnd, () => {
                const d = new Date(tournament.startDate);
                d.setDate(d.getDate() - 8);
                return d;
            });

            const advancedStart = getDateValue(tournament.advancedStart, () => {
                const d = new Date(tournament.startDate);
                d.setDate(d.getDate() - 7);
                return d;
            });

            const advancedEnd = getDateValue(tournament.advancedEnd, () => {
                const d = new Date(tournament.startDate);
                d.setDate(d.getDate() - 1);
                return d;
            });

            const teardownStart = getDateValue(tournament.teardownStart, () => {
                const d = new Date(tournament.endDate);
                d.setDate(d.getDate() + 1);
                return d;
            });

            const teardownEnd = getDateValue(tournament.teardownEnd, () => {
                const d = new Date(tournament.endDate);
                d.setDate(d.getDate() + 6);
                return d;
            });

            // Setup phase
            const setupDays = Math.max(1, Math.ceil((setupEnd - setupStart) / (1000 * 60 * 60 * 24)) + 1);
            for (let i = 0; i < setupDays; i++) {
                const date = new Date(setupStart);
                date.setDate(date.getDate() + i);
                addActivity(date.toDateString(), {
                    date: new Date(date),
                    phase: "setup",
                    tournament: tournament,
                    dayInPhase: i + 1,
                    totalInPhase: setupDays,
                });
            }

            // Advanced week
            const advancedDays = Math.max(1, Math.ceil((advancedEnd - advancedStart) / (1000 * 60 * 60 * 24)) + 1);
            for (let i = 0; i < advancedDays; i++) {
                const date = new Date(advancedStart);
                date.setDate(date.getDate() + i);
                addActivity(date.toDateString(), {
                    date: new Date(date),
                    phase: "advanced",
                    tournament: tournament,
                    dayInPhase: i + 1,
                    totalInPhase: advancedDays,
                });
            }

            // Tournament days
            const tournamentDays = Math.max(1, Math.ceil((tournament.endDate - tournament.startDate) / (1000 * 60 * 60 * 24)) + 1);
            for (let i = 0; i < tournamentDays; i++) {
                const date = new Date(tournament.startDate);
                date.setDate(date.getDate() + i);
                addActivity(date.toDateString(), {
                    date: new Date(date),
                    phase: "tournament",
                    tournament: tournament,
                    dayInPhase: i + 1,
                    totalInPhase: tournamentDays,
                });
            }

            // Teardown phase
            const teardownDays = Math.max(1, Math.ceil((teardownEnd - teardownStart) / (1000 * 60 * 60 * 24)) + 1);
            for (let i = 0; i < teardownDays; i++) {
                const date = new Date(teardownStart);
                date.setDate(date.getDate() + i);
                addActivity(date.toDateString(), {
                    date: new Date(date),
                    phase: "teardown",
                    tournament: tournament,
                    dayInPhase: i + 1,
                    totalInPhase: teardownDays,
                });
            }
        }

        // Generate all days of the year using the pre-built tournamentActivities map
        // This is O(days) instead of O(days × tournaments)
        for (let day = 1; day <= this.daysInYear; day++) {
            const date = this.getDateFromDayOfYear(day);
            const dateKey = date.toDateString();

            // Look up activities from the pre-built map (O(1) lookup)
            const activities = tournamentActivities.get(dateKey);

            if (activities && activities.length > 0) {
                // Use first activity as primary for legacy compatibility
                const primaryActivity = activities[0];
                this.scheduleRows.push({
                    date: date,
                    phase: primaryActivity.phase,
                    tournament: primaryActivity.tournament,
                    dayInPhase: primaryActivity.dayInPhase,
                    totalInPhase: primaryActivity.totalInPhase,
                    allTournaments: activities, // All overlapping tournaments for this date
                });
            } else {
                // Regular day with no tournament activity
                this.scheduleRows.push({
                    date: date,
                    phase: null,
                    tournament: null,
                    dayInPhase: null,
                    totalInPhase: null,
                    allTournaments: [],
                });
            }
        }

        // Store complete schedule 
        this.allScheduleRows = [...this.scheduleRows];
    }

    applyTournamentFilter() {
        if (this.filteredTournamentIds.size === 0) {
            // No filter applied, show all rows with all tournament activities
            this.scheduleRows = [...this.allScheduleRows];
            this.filteredData = null; // Clear filtered data
            return;
        }

        // Calculate date range for selected tournaments with 7-day padding
        let earliestDate = null;
        let latestDate = null;

        this.tournaments.forEach(tournament => {
            if (this.filteredTournamentIds.has(tournament.id)) {
                // Get the full tournament range (setup to teardown)
                const setupStart = tournament.setupStart ?
                    (tournament.setupStart instanceof Date ? tournament.setupStart : new Date(tournament.setupStart)) :
                    (() => {
                        const date = new Date(tournament.startDate);
                        date.setDate(date.getDate() - 21);
                        return date;
                    })();

                const teardownEnd = tournament.teardownEnd ?
                    (tournament.teardownEnd instanceof Date ? tournament.teardownEnd : new Date(tournament.teardownEnd)) :
                    (() => {
                        const date = new Date(tournament.endDate);
                        date.setDate(date.getDate() + 6);
                        return date;
                    })();

                if (!earliestDate || setupStart < earliestDate) {
                    earliestDate = new Date(setupStart);
                }
                if (!latestDate || teardownEnd > latestDate) {
                    latestDate = new Date(teardownEnd);
                }
            }
        });

        if (earliestDate && latestDate) {
            // Add 7-day padding to the range
            earliestDate.setDate(earliestDate.getDate() - 7);
            latestDate.setDate(latestDate.getDate() + 7);

            // Filter schedule rows to only include dates in the range
            this.scheduleRows = this.allScheduleRows.filter(row => {
                return row.date >= earliestDate && row.date <= latestDate;
            });

            // Filter tournament activities within each row
            this.scheduleRows = this.scheduleRows.map(row => {
                const filteredRow = { ...row };

                if (row.allTournaments && row.allTournaments.length > 0) {
                    // Filter allTournaments to only include selected tournaments
                    filteredRow.allTournaments = row.allTournaments.filter(activity =>
                        this.filteredTournamentIds.has(activity.tournament.id)
                    );

                    // Update primary tournament data based on filtered tournaments
                    if (filteredRow.allTournaments.length > 0) {
                        const primaryActivity = filteredRow.allTournaments[0];
                        filteredRow.tournament = primaryActivity.tournament;
                        filteredRow.phase = primaryActivity.phase;
                        filteredRow.dayInPhase = primaryActivity.dayInPhase;
                        filteredRow.totalInPhase = primaryActivity.totalInPhase;
                    } else {
                        // No filtered tournaments for this row, make it a regular day
                        filteredRow.tournament = null;
                        filteredRow.phase = null;
                        filteredRow.dayInPhase = null;
                        filteredRow.totalInPhase = null;
                    }
                }

                return filteredRow;
            });

            // Filter cell data to hide assignments for non-selected tournaments
            this.filterCellAssignments();
        } else {
            // No matching tournaments found, show empty schedule
            this.scheduleRows = [];
        }
    }

    filterCellAssignments() {
        console.log('[FILTER DEBUG] filterCellAssignments() called');
        console.log('[FILTER DEBUG] scheduleRows.length:', this.scheduleRows.length);
        console.log('[FILTER DEBUG] allScheduleRows.length:', this.allScheduleRows.length);

        // Create a mapping from original row indices to filtered row indices
        const rowMapping = new Map();

        this.scheduleRows.forEach((filteredRow, filteredIndex) => {
            // Find the original row index for this date
            const originalRowIndex = this.allScheduleRows.findIndex(originalRow =>
                originalRow.date.getTime() === filteredRow.date.getTime()
            );

            if (originalRowIndex !== -1) {
                rowMapping.set(originalRowIndex, filteredIndex);
            }
        });

        console.log('[FILTER DEBUG] rowMapping size:', rowMapping.size);
        console.log('[FILTER DEBUG] First 5 mappings:', Array.from(rowMapping.entries()).slice(0, 5));

        // Create a map of filtered cell data with remapped positions
        const filteredData = {};
        let remappedCount = 0;

        Object.keys(this.data).forEach(cellId => {
            const cellData = this.data[cellId];
            const [col, originalRow] = cellId.split('_').map(Number);

            // Check if this row is included in the filtered view
            const newRow = rowMapping.get(originalRow);

            if (newRow !== undefined) {
                // Row is included in filtered view, create new cell ID
                const newCellId = `${col}_${newRow}`;

                // Debug first few remappings
                if (remappedCount < 5 && cellData.tournamentId) {
                    console.log(`[FILTER DEBUG] Remapping: ${cellId} -> ${newCellId} (originalRow=${originalRow}, newRow=${newRow})`);
                    remappedCount++;
                }

                // Check both tournament and tournamentId properties
                const tournamentId = cellData.tournamentId || cellData.tournament;

                if (tournamentId) {
                    // Check if this is a tournament-related cell
                    if (cellData.assigned && !this.filteredTournamentIds.has(tournamentId)) {
                        // Assignment to filtered-out tournament - clear the cell completely
                        filteredData[newCellId] = {
                            value: "",
                            originalValue: "",
                            style: cellData.style || {},
                            assigned: false,
                            tournament: null,
                            phase: null,
                            notWorking: false,
                            travel: false,
                            role: null
                        };
                    } else if (cellData.notWorking && !this.filteredTournamentIds.has(tournamentId)) {
                        // Not working for filtered-out tournament - clear the cell completely
                        filteredData[newCellId] = {
                            value: "",
                            originalValue: "",
                            style: cellData.style || {},
                            assigned: false,
                            tournament: null,
                            phase: null,
                            notWorking: false,
                            travel: false,
                            role: null
                        };
                    } else if (this.filteredTournamentIds.has(tournamentId)) {
                        // Tournament is selected, keep the assignment/not working status
                        filteredData[newCellId] = { ...cellData };
                    } else {
                        // Other tournament-related data for filtered tournaments
                        filteredData[newCellId] = {
                            value: "",
                            originalValue: "",
                            style: cellData.style || {},
                            assigned: false,
                            tournament: null,
                            phase: null,
                            notWorking: false,
                            travel: false,
                            role: null
                        };
                    }
                } else {
                    // Not tournament-related (travel, regular data, etc.), keep as is
                    filteredData[newCellId] = { ...cellData };
                }
            }
            // If row is not in filtered view, the cell data is simply not included
        });

        // Store the filtered data temporarily (we'll apply it during grid creation)
        this.filteredData = filteredData;

        console.log('[FILTER DEBUG] filteredData created with', Object.keys(filteredData).length, 'entries');
        console.log('[FILTER DEBUG] First 10 cellIds in filteredData:', Object.keys(filteredData).slice(0, 10));
        console.log('[FILTER DEBUG] Sample filteredData entries:', Object.entries(filteredData).slice(0, 3).map(([id, data]) => `${id}: ${data.value || data.tournament || 'empty'}`));
    }

    getDateFromDayOfYear(dayOfYear) {
        // Create January 1st of the year, then add (dayOfYear - 1) days
        const date = new Date(this.year, 0, 1);
        date.setDate(date.getDate() + (dayOfYear - 1));
        return date;
    }

    calculateTournamentSpans() {
        const spans = [];
        let currentTournamentId = null;
        let currentTournament = null;
        let currentSpanStart = 0;
        let currentSpanCount = 0;

        for (let rowIndex = 0; rowIndex < this.scheduleRows.length; rowIndex++) {
            const rowData = this.scheduleRows[rowIndex];

            if (rowData.tournament) {
                if (
                    currentTournamentId &&
                    currentTournamentId === rowData.tournament.id
                ) {
                    // Continue current tournament span (all phases of same tournament)
                    currentSpanCount++;
                } else {
                    // End previous span (if any)
                    if (currentTournamentId || currentSpanCount > 0) {
                        spans.push({
                            tournament: currentTournament,
                            startRow: currentSpanStart,
                            rowSpan: currentSpanCount,
                        });
                    }

                    // Start new tournament span
                    currentTournamentId = rowData.tournament.id;
                    currentTournament = rowData.tournament;
                    currentSpanStart = rowIndex;
                    currentSpanCount = 1;
                }
            } else {
                // Regular day (no tournament)
                if (currentTournamentId) {
                    // End tournament span
                    spans.push({
                        tournament: currentTournament,
                        startRow: currentSpanStart,
                        rowSpan: currentSpanCount,
                    });
                    currentTournamentId = null;
                    currentTournament = null;
                    currentSpanStart = rowIndex;
                    currentSpanCount = 1;
                } else if (currentSpanCount === 0) {
                    // Start regular days span
                    currentSpanStart = rowIndex;
                    currentSpanCount = 1;
                } else {
                    // Continue regular days span
                    currentSpanCount++;
                }
            }
        }

        // End final span
        if (currentSpanCount > 0) {
            spans.push({
                tournament: currentTournament,
                startRow: currentSpanStart,
                rowSpan: currentSpanCount,
            });
        }

        return spans;
    }

    init() {
        this.populateYearSelector();
        this.createGrid();
        // Start with first tournament row if available, otherwise first row
        if (this.scheduleRows.length > 0) {
            const firstTournamentRow = this.scheduleRows.find(
                (row) => row.tournament
            );
            if (firstTournamentRow) {
                const reference = `${this.columnNames[0]} - ${firstTournamentRow.tournament.name
                    } (${firstTournamentRow.phase.toUpperCase()}) - ${firstTournamentRow.date.toLocaleDateString()}`;
                this.updateCellReference(reference);
            } else {
                // No tournaments scheduled, show first regular day
                const firstRow = this.scheduleRows[0];
                const reference = `${this.columnNames[0]
                    } - ${firstRow.date.toLocaleDateString()}`;
                this.updateCellReference(reference);
            }
        } else {
            this.updateCellReference("No schedule data available");
        }
    }

    createGrid() {

        const spreadsheet = $("#spreadsheet");
        spreadsheet.empty();

        const colCount = this.columnNames.length;
        const rowCount = this.scheduleRows.length;
        const vs = this.virtualScrolling;

        // Pre-compute all row metadata once (tournament info, colors, etc.)
        this._precomputeRowData();

        // Pre-compute tournament spans for the sidebar
        this._computeTournamentSpans();

        // Set up grid structure for virtual scrolling
        const totalHeight = vs.headerHeight + (rowCount * vs.rowHeight);
        const totalWidth = 300 + 120 + (colCount * 140); // tournament + date + cells

        spreadsheet.css({
            "display": "block",
            "position": "relative",
            "height": totalHeight + "px",
            "width": totalWidth + "px"
        });

        // Create fixed header row
        let headersHtml = `<div class="virtual-header" style="position: sticky; top: 0; z-index: 200; display: grid; grid-template-columns: 300px 120px repeat(${colCount}, 140px); height: ${vs.headerHeight}px; background: #f1f3f4;">`;
        headersHtml += '<div class="header-cell" style="position: sticky; left: 0; z-index: 201; background: #f1f3f4;">Tournament</div>';
        headersHtml += '<div class="header-cell" style="position: sticky; left: 300px; z-index: 201; background: #f1f3f4;">Date</div>';
        for (let col = 0; col < colCount; col++) {
            // Skip this column if filter is active and user is not assigned
            if (this.showOnlyAssignedUsers && !this.assignedUserColumns.has(col)) {
                continue;
            }

            const name = this.columnNames[col];
            headersHtml += `<div class="header-cell col-header" data-col="${col}" title="${name}">${name}</div>`;
        }
        headersHtml += '</div>';

        // Main content area with tournament sidebar and virtual rows
        headersHtml += `<div style="display: grid; grid-template-columns: 300px 1fr; height: ${rowCount * vs.rowHeight}px;">`;

        // Tournament sidebar (sticky left)
        headersHtml += `<div id="tournament-sidebar" style="position: sticky; left: 0; z-index: 100; background: white; height: ${rowCount * vs.rowHeight}px;"></div>`;

        // Virtual scroll container for date column and cells
        headersHtml += `<div id="virtual-row-container" style="position: relative; height: ${rowCount * vs.rowHeight}px; width: ${120 + (colCount * 140)}px;"></div>`;

        headersHtml += '</div>';

        spreadsheet.html(headersHtml);

        // Store references
        this._rowContainer = document.getElementById('virtual-row-container');
        this._tournamentSidebar = document.getElementById('tournament-sidebar');

        // Render the tournament sidebar (static, not virtualized)
        this._renderTournamentSidebar();

        // Set up scroll handler
        this._setupVirtualScroll();

        // Initial render of visible rows
        this._renderVisibleRows();

        // Hide loading indicator
        if (window.hideLoading) {
            window.hideLoading();
        }
    }

    // Identify which users (columns) have any schedule entries
    // Respects tournament filter and current year
    getAssignedUserColumns() {
        const assignedCols = new Set();
        const hasTournamentFilter = this.filteredTournamentIds.size > 0;

        // Create a set of visible row indices (from scheduleRows)
        const visibleRowIndices = new Set();
        if (hasTournamentFilter && this.allScheduleRows && this.scheduleRows) {
            // Map scheduleRows (filtered) back to allScheduleRows (complete) indices
            this.scheduleRows.forEach(filteredRow => {
                const originalIndex = this.allScheduleRows.findIndex(row =>
                    row.date.getTime() === filteredRow.date.getTime()
                );
                if (originalIndex !== -1) {
                    visibleRowIndices.add(originalIndex);
                }
            });
        }

        // Iterate through all cell data
        Object.entries(this.data).forEach(([cellId, cellData]) => {
            if (!cellData) return;

            // Check if this cell represents an assignment
            const hasAssignment = cellData.tournamentId ||
                cellData.travel ||
                cellData.isTravel ||
                cellData.notWorking;

            if (!hasAssignment) return;

            // Extract column and row index from cellId (format: "colIndex_rowIndex")
            const [colIndex, rowIndex] = cellId.split('_').map(Number);

            // If tournament filter is active, only count assignments on visible rows
            if (hasTournamentFilter) {
                // Row must be visible in the filtered schedule
                if (!visibleRowIndices.has(rowIndex)) {
                    return; // Skip assignments on filtered-out rows
                }

                // Must have a tournamentId to be filtered
                if (!cellData.tournamentId) return;

                // Must be in the filtered tournament set
                if (!this.filteredTournamentIds.has(cellData.tournamentId)) {
                    return; // Skip assignments for filtered-out tournaments
                }
            }

            assignedCols.add(colIndex);
        });

        return assignedCols;
    }

    // Toggle the assigned users filter on/off
    toggleAssignedUsersFilter() {
        // Toggle state
        this.showOnlyAssignedUsers = !this.showOnlyAssignedUsers;

        if (this.showOnlyAssignedUsers) {
            // Get assigned user column indices
            this.assignedUserColumns = this.getAssignedUserColumns();

            // Update button to active state
            $("#assigned-users-toggle")
                .removeClass('btn-outline-secondary')
                .addClass('btn-secondary active');
        } else {
            // Clear filter
            this.assignedUserColumns.clear();

            // Update button to inactive state
            $("#assigned-users-toggle")
                .removeClass('btn-secondary active')
                .addClass('btn-outline-secondary');
        }

        // Re-render grid
        this.createGrid();
    }

    // Count assignments per column
    countAssignmentsPerColumn() {
        const assignmentCounts = new Map();
        const hasTournamentFilter = this.filteredTournamentIds.size > 0;

        // Initialize counts for all columns
        for (let i = 0; i < this.columnNames.length; i++) {
            assignmentCounts.set(i, 0);
        }

        // Build visible row indices if tournament filter is active
        const visibleRowIndices = new Set();
        if (hasTournamentFilter && this.allScheduleRows && this.scheduleRows) {
            this.scheduleRows.forEach(filteredRow => {
                const originalIndex = this.allScheduleRows.findIndex(row =>
                    row.date.getTime() === filteredRow.date.getTime()
                );
                if (originalIndex !== -1) {
                    visibleRowIndices.add(originalIndex);
                }
            });
        }

        // Count assignments for each column
        Object.entries(this.data).forEach(([cellId, cellData]) => {
            if (!cellData) return;

            // Check if this cell has a tournament assignment or travel day
            // (excludes sick days and time offs which have notWorking: true)
            const hasAssignment = (cellData.assigned && cellData.tournamentId) ||
                cellData.travel ||
                cellData.isTravel;

            if (!hasAssignment) return;

            // Extract column and row index
            const [colIndex, rowIndex] = cellId.split('_').map(Number);

            // If tournament filter is active, only count assignments on visible rows
            if (hasTournamentFilter) {
                // Row must be visible in the filtered schedule
                if (!visibleRowIndices.has(rowIndex)) {
                    return;
                }

                // Must have a tournamentId to be filtered
                if (!cellData.tournamentId) return;

                // Must be in the filtered tournament set
                if (!this.filteredTournamentIds.has(cellData.tournamentId)) {
                    return;
                }
            }

            if (assignmentCounts.has(colIndex)) {
                assignmentCounts.set(colIndex, assignmentCounts.get(colIndex) + 1);
            }
        });

        return assignmentCounts;
    }

    // Sort columns by assignment count (most assignments first)
    sortColumnsByAssignmentCount() {
        // Get assignment counts
        const assignmentCounts = this.countAssignmentsPerColumn();

        // Create array of column indices with their names and counts
        const columnData = this.columnNames.map((name, index) => ({
            name,
            originalIndex: index,
            count: assignmentCounts.get(index) || 0,
            isTBD: name.toLowerCase().startsWith('tbd')
        }));

        // Sort with three tiers:
        // 1. Non-TBD people with assignments (count > 0), sorted by count desc, then name asc
        // 2. TBD entries (regardless of assignments), sorted by count desc, then name asc
        // 3. Non-TBD people with no assignments, sorted alphabetically
        columnData.sort((a, b) => {
            const aHasAssignments = a.count > 0;
            const bHasAssignments = b.count > 0;

            // Determine tier: 1 = non-TBD with assignments, 2 = TBD, 3 = non-TBD without assignments
            const getTier = (item) => {
                if (item.isTBD) return 2;
                if (item.count > 0) return 1;
                return 3;
            };

            const aTier = getTier(a);
            const bTier = getTier(b);

            // Sort by tier first
            if (aTier !== bTier) return aTier - bTier;

            // Within same tier: sort by count desc, then name asc
            if (a.count !== b.count) {
                return b.count - a.count;
            }
            return a.name.localeCompare(b.name);
        });

        // Create mapping from old column index to new column index
        const indexMapping = new Map();
        columnData.forEach((col, newIndex) => {
            indexMapping.set(col.originalIndex, newIndex);
        });

        // Update columnNames array
        this.columnNames = columnData.map(col => col.name);

        // Remap all cell data to new column indices
        const newData = {};
        Object.entries(this.data).forEach(([cellId, cellData]) => {
            const [oldColIndex, rowIndex] = cellId.split('_').map(Number);
            const newColIndex = indexMapping.get(oldColIndex);
            if (newColIndex !== undefined) {
                const newCellId = `${newColIndex}_${rowIndex}`;
                newData[newCellId] = cellData;
            }
        });

        this.data = newData;

        // Clear filteredData and regenerate it if tournament filter is active
        this.filteredData = null;
        if (this.filteredTournamentIds.size > 0) {
            this.filterCellAssignments();
        }

        // Re-render grid with new column order
        this.createGrid();
    }

    _computeTournamentSpans() {
        // Calculate continuous tournament blocks for the sidebar
        this._tournamentSpans = [];
        const vs = this.virtualScrolling;
        const cachedColorMap = this._renderContext.cachedColorMap;

        let currentSpan = null;

        for (let rowIndex = 0; rowIndex < this.scheduleRows.length; rowIndex++) {
            const rowData = this.scheduleRows[rowIndex];

            // Get tournament IDs for this row (sorted for consistent comparison)
            const tournamentIds = (rowData.allTournaments || [])
                .map(t => t.tournament.id)
                .sort((a, b) => a - b)
                .join(',');

            if (currentSpan && currentSpan.tournamentIds === tournamentIds) {
                // Extend current span
                currentSpan.endRow = rowIndex;
            } else {
                // Start new span
                if (currentSpan) {
                    this._tournamentSpans.push(currentSpan);
                }
                currentSpan = {
                    startRow: rowIndex,
                    endRow: rowIndex,
                    tournamentIds: tournamentIds,
                    tournaments: rowData.allTournaments || []
                };
            }
        }

        // Don't forget the last span
        if (currentSpan) {
            this._tournamentSpans.push(currentSpan);
        }
    }

    _renderTournamentSidebar() {
        if (!this._tournamentSidebar) return;

        const vs = this.virtualScrolling;
        const cachedColorMap = this._renderContext.cachedColorMap;
        const allTournamentIds = this.tournaments.map(t => t.id).sort((a, b) => a - b);

        let sidebarHtml = '';

        for (const span of this._tournamentSpans) {
            const top = span.startRow * vs.rowHeight;
            const height = (span.endRow - span.startRow + 1) * vs.rowHeight;

            let spanContent = '';

            if (span.tournaments.length > 0) {
                const activeTournamentIds = new Set(span.tournaments.map(t => t.tournament.id));

                // Render all tournament slots for consistent alignment
                spanContent = allTournamentIds.map(tournamentId => {
                    const background = cachedColorMap.get(tournamentId);
                    if (activeTournamentIds.has(tournamentId)) {
                        const tournament = span.tournaments.find(t => t.tournament.id === tournamentId)?.tournament;
                        const textColor = this.getTextColor(background);
                        return `<div class="tournament-bar" style="background: ${background}; flex: 1; height: 100%; font-size: 10px; padding: 4px 2px; color: ${textColor}; text-shadow: 1px 1px 1px rgba(0,0,0,0.3); writing-mode: vertical-rl; text-orientation: mixed; overflow: hidden;" title="${tournament?.name || ''} (${tournament?.location || ''})">${tournament?.name || ''}</div>`;
                    } else {
                        return `<div class="tournament-bar" style="flex: 1; height: 100%; opacity: 0;"></div>`;
                    }
                }).join('');
            }

            sidebarHtml += `<div class="tournament-span" style="position: absolute; top: ${top}px; left: 0; right: 0; height: ${height}px; display: flex; border-bottom: 1px solid #e0e0e0;">${spanContent}</div>`;
        }

        this._tournamentSidebar.innerHTML = sidebarHtml;
    }

    _precomputeRowData() {
        const colCount = this.columnNames.length;
        const dataSource = this.filteredData || this.data;
        const cachedColorMap = this.getTournamentColorMap();
        const tournamentLookup = new Map(this.tournaments.map(t => [t.id, t]));

        // Store for use in rendering
        this._renderContext = {
            colCount,
            dataSource,
            cachedColorMap,
            tournamentLookup
        };
    }

    _setupVirtualScroll() {
        const container = document.querySelector('.spreadsheet-container');
        if (!container) return;

        const vs = this.virtualScrolling;

        container.addEventListener('scroll', () => {
            if (vs.scrollRAF) return; // Already scheduled

            vs.scrollRAF = requestAnimationFrame(() => {
                vs.scrollRAF = null;
                this._renderVisibleRows();
            });
        }, { passive: true });
    }

    _renderVisibleRows() {
        const container = document.querySelector('.spreadsheet-container');
        if (!container || !this._rowContainer) return;

        const vs = this.virtualScrolling;
        const scrollTop = container.scrollTop;
        const viewportHeight = container.clientHeight;

        // Calculate visible row range
        const firstVisibleRow = Math.max(0, Math.floor((scrollTop - vs.headerHeight) / vs.rowHeight) - vs.bufferRows);
        const lastVisibleRow = Math.min(
            this.scheduleRows.length - 1,
            Math.ceil((scrollTop - vs.headerHeight + viewportHeight) / vs.rowHeight) + vs.bufferRows
        );

        // Check if we need to re-render
        if (firstVisibleRow === vs.renderedRange.start && lastVisibleRow === vs.renderedRange.end) {
            return; // No change needed
        }

        vs.renderedRange.start = firstVisibleRow;
        vs.renderedRange.end = lastVisibleRow;

        // Render the visible rows
        const { colCount, dataSource, cachedColorMap, tournamentLookup } = this._renderContext;
        const rowsHtml = [];

        for (let rowIndex = firstVisibleRow; rowIndex <= lastVisibleRow; rowIndex++) {
            const rowData = this.scheduleRows[rowIndex];
            const topPosition = rowIndex * vs.rowHeight;

            // Build row header info
            let phaseClass = 'regular-day';
            let tournamentId = '';
            let phase = '';
            let dateString = this.formatTournamentDate(rowData);
            let tooltipHtml = '';

            // Always build tooltip from allTournaments to show ALL tournaments on this date
            if (rowData.allTournaments && rowData.allTournaments.length > 0) {
                // Build tooltip showing all tournaments on this date
                tooltipHtml = rowData.allTournaments.map(t => {
                    const phaseLabel = { setup: "Setup", advanced: "Adv", tournament: "Event", teardown: "Teardown" }[t.phase] || t.phase;
                    return `${t.tournament.name} (${phaseLabel})`;
                }).join('\n');

                if (rowData.allTournaments.length > 1) {
                    phaseClass = 'regular-day overlapping-tournaments';
                }
            }

            // Set phase class for single tournament rows
            if (rowData.tournament) {
                phaseClass = `${rowData.phase}-phase`;
                tournamentId = rowData.tournament.id;
                phase = rowData.phase;
            }

            // Build cells HTML
            const dateIso = rowData.date.toISOString();
            let cellsHtml = '';

            for (let col = 0; col < colCount; col++) {
                // Skip this column if filter is active and user is not assigned
                if (this.showOnlyAssignedUsers && !this.assignedUserColumns.has(col)) {
                    continue;
                }

                const columnName = this.columnNames[col];
                const cellId = `${col}_${rowIndex}`;
                const cellData = dataSource[cellId] || {};

                // Debug logging for first few visible columns on first visible row
                if (rowIndex === firstVisibleRow && col < 5 && this.showOnlyAssignedUsers) {
                    console.log(`[CELL DEBUG] col=${col}, cellId=${cellId}, has data=${!!dataSource[cellId]}, cellData.tournamentId=${cellData.tournamentId}, cellData.assigned=${cellData.assigned}`);
                }

                const classes = ['cell'];
                if (cellData.assigned) classes.push('assigned');
                if (cellData.notWorking) classes.push('not-working');
                if (cellData.travel || cellData.isTravel) classes.push('travel');
                if (cellData.role) {
                    classes.push(`role-${cellData.role.toLowerCase().replace(/\s+/g, "-")}`);
                }

                const styles = [];
                // Apply tournament colors only for assigned cells (not travel/not-working which have their own CSS)
                // Use !important to override the CSS .cell.assigned defaults
                if (cellData.tournament && cellData.assigned && !cellData.notWorking && !cellData.travel && !cellData.isTravel) {
                    const tournamentColor = cachedColorMap.get(cellData.tournament);
                    if (tournamentColor) {
                        const borderColor = tournamentColor.match(/#([0-9a-f]{6})/i)?.[1] || "667eea";
                        const textColor = this.getTextColor(tournamentColor);
                        styles.push(`background: ${tournamentColor} !important`);
                        styles.push(`color: ${textColor} !important`);
                        styles.push(`border-color: #${borderColor} !important`);
                    }
                    // Add phase border color
                    if (cellData.phase) {
                        const phaseBorderColors = {
                            setup: '#000000',
                            advanced: '#334155',
                            tournament: '#FFFFFF',
                            teardown: '#6B7280'
                        };
                        const phaseBorderColor = phaseBorderColors[cellData.phase];
                        if (phaseBorderColor) {
                            styles.push(`border-left: 4px solid ${phaseBorderColor} !important`);
                        }
                    }
                }

                // Apply tournament colors for travel cells based on destination tournament
                if (cellData.travel || cellData.isTravel) {
                    // Determine destination tournament: use travelTournamentId if set, otherwise tournamentId
                    const destinationTournamentId = cellData.travelTournamentId || cellData.tournamentId;
                    if (destinationTournamentId) {
                        const tournamentColor = cachedColorMap.get(destinationTournamentId);
                        if (tournamentColor) {
                            const borderColor = tournamentColor.match(/#([0-9a-f]{6})/i)?.[1] || "667eea";
                            const textColor = this.getTextColor(tournamentColor);
                            styles.push(`background: ${tournamentColor} !important`);
                            styles.push(`color: ${textColor} !important`);
                            styles.push(`border-color: #${borderColor} !important`);
                        }
                    }
                }

                let cellContent = '';
                if (cellData.assigned && cellData.tournament && !cellData.notWorking && !cellData.travel && !cellData.isTravel) {
                    const tournament = tournamentLookup.get(cellData.tournament);
                    if (tournament) {
                        const phaseNames = { setup: "Setup", advanced: "Adv", tournament: "Event", teardown: "TD" };
                        const phaseDisplay = phaseNames[cellData.phase] || cellData.phase || "Work";
                        const roleDisplay = cellData.role ? `<div class="role-badge">${this.getRoleAbbreviation ? this.getRoleAbbreviation(cellData.role) : cellData.role}</div>` : '';
                        cellContent = `<div class="cell-content-wrapper"><div class="cell-main-content"><div class="tournament-info">${tournament.name}</div><div class="assignment-status">${phaseDisplay}</div></div>${roleDisplay}</div>`;
                    }
                } else if (cellData.notWorking) {
                    // Determine absence type label based on EntryType
                    let absenceLabel = 'Not Working';  // Default for backward compatibility
                    switch(cellData.entryType) {
                        case 3:
                            absenceLabel = 'Sick';
                            break;
                        case 4:
                            absenceLabel = 'Time Off - Offsite';
                            break;
                        case 5:
                            absenceLabel = 'Time Off - Onsite';
                            break;
                    }

                    const tournament = cellData.tournament ? tournamentLookup.get(cellData.tournament) : null;
                    if (tournament) {
                        cellContent = `<div class="tournament-info">${tournament.name}</div><div class="assignment-status">${absenceLabel}</div>`;
                    } else {
                        cellContent = `<div class="assignment-status">${absenceLabel}</div>`;
                    }
                } else if (cellData.travel || cellData.isTravel) {
                    const destinationTournamentId = cellData.travelTournamentId || cellData.tournamentId;
                    const tournament = destinationTournamentId ? tournamentLookup.get(destinationTournamentId) : null;
                    if (tournament) {
                        cellContent = `<div class="tournament-info">${tournament.name}</div><div class="assignment-status">Travel</div>`;
                    } else {
                        cellContent = '<div class="assignment-status">Travel</div>';
                    }
                }

                cellsHtml += `<div class="${classes.join(' ')}" data-cell="${cellId}" data-row="${rowIndex}" data-col="${col}" data-date="${dateIso}" data-name="${columnName}"${styles.length ? ` style="${styles.join('; ')}"` : ''}><span class="cell-display">${cellContent}</span></div>`;
            }

            // Build complete row HTML with absolute positioning
            // Tournament sidebar is rendered separately, so we only have date column + cells here
            const titleAttr = tooltipHtml ? ` title="${tooltipHtml}"` : '';
            rowsHtml.push(`<div class="virtual-row" style="position: absolute; top: ${topPosition}px; left: 0; right: 0; height: ${vs.rowHeight}px; display: grid; grid-template-columns: 120px repeat(${colCount}, 140px);">
                <div class="row-header phase-header ${phaseClass}" data-row="${rowIndex}" data-tournament="${tournamentId}" data-phase="${phase}"${titleAttr}>${dateString}</div>
                ${cellsHtml}
            </div>`);
        }

        // Replace container content
        this._rowContainer.innerHTML = rowsHtml.join('');
    }


    initializeTooltips() {
        // Dispose of existing tooltips first
        $('[data-bs-toggle="tooltip"]').tooltip('dispose');

        // Initialize new tooltips
        $('[data-bs-toggle="tooltip"]').tooltip({
            container: 'body',
            trigger: 'hover focus'
        });
    }

    bindEvents() {
        const self = this;

        // Cell selection and editing
        $(document).on("click", ".cell", function (e) {
            e.stopPropagation();

            // Hide context menu when clicking on cells
            self.hideContextMenu();

            // Don't process click if we just finished a drag selection
            if (self.justFinishedDragging) {
                self.justFinishedDragging = false;
                return;
            }

            if (e.shiftKey && self.selectedCell) {
                // Shift-click for range selection
                self.selectRange(self.selectedCell, $(this));
            } else if (e.ctrlKey || e.metaKey) {
                // Ctrl/Cmd-click for multi-selection
                self.toggleCellSelection($(this));
            } else {
                // Normal click
                self.selectCell($(this));
            }
        });

        // Drag selection
        $(document).on("mousedown", ".cell", function (e) {
            if (e.which === 1 && !e.shiftKey && !e.ctrlKey && !e.metaKey) {
                // Left mouse button down without modifiers - start drag selection
                self.isSelecting = true;
                self.selectionStart = $(this);
                self.selectionEnd = $(this);
                self.dragMoved = false; // Track if we actually dragged

                // Prevent text selection during drag
                e.preventDefault();
                $("body").addClass("no-select");
            }
        });

        // Auto-scroll configuration for drag selection
        self.autoScrollSpeed = 15; // Pixels to scroll per frame
        self.autoScrollThreshold = 50; // Distance from edge to trigger auto-scroll
        self.autoScrollInterval = null;

        $(document).on("mousemove", function (e) {
            if (self.isSelecting && self.selectionStart) {
                const container = $(".spreadsheet-container")[0];
                if (!container) return;

                const containerRect = container.getBoundingClientRect();
                const mouseX = e.clientX;
                const mouseY = e.clientY;

                // Calculate auto-scroll direction
                let scrollX = 0;
                let scrollY = 0;

                if (mouseY < containerRect.top + self.autoScrollThreshold) {
                    scrollY = -self.autoScrollSpeed;
                } else if (mouseY > containerRect.bottom - self.autoScrollThreshold) {
                    scrollY = self.autoScrollSpeed;
                }

                if (mouseX < containerRect.left + self.autoScrollThreshold) {
                    scrollX = -self.autoScrollSpeed;
                } else if (mouseX > containerRect.right - self.autoScrollThreshold) {
                    scrollX = self.autoScrollSpeed;
                }

                // Start or stop auto-scrolling
                if (scrollX !== 0 || scrollY !== 0) {
                    if (!self.autoScrollInterval) {
                        self.autoScrollInterval = setInterval(() => {
                            container.scrollLeft += scrollX;
                            container.scrollTop += scrollY;
                            // Update selection based on current mouse position
                            self.updateSelectionFromPoint(e.clientX, e.clientY);
                        }, 16); // ~60fps
                    }
                } else {
                    if (self.autoScrollInterval) {
                        clearInterval(self.autoScrollInterval);
                        self.autoScrollInterval = null;
                    }
                }

                // Update selection based on mouse position
                self.updateSelectionFromPoint(mouseX, mouseY);

                e.preventDefault();
            }
        });

        $(document).on("mouseup", function (e) {
            if (self.isSelecting) {
                // End drag selection
                self.isSelecting = false;
                $("body").removeClass("no-select");

                // Stop auto-scrolling
                if (self.autoScrollInterval) {
                    clearInterval(self.autoScrollInterval);
                    self.autoScrollInterval = null;
                }

                // Only set flag if we actually dragged (moved to a different cell)
                if (self.dragMoved) {
                    self.justFinishedDragging = true;

                    // Reset the flag after a short delay to allow normal clicks again
                    setTimeout(() => {
                        self.justFinishedDragging = false;
                    }, 50);
                }

                // Clean up the selection state
                self.selectionStart = null;
                self.selectionEnd = null;
                self.dragMoved = false;
            }
        });

        // Prevent dragging from interfering with text selection in other elements
        $(document).on("dragstart", function (e) {
            if (self.isSelecting) {
                e.preventDefault();
            }
        });

        $(document).on("dblclick", ".cell", function () {
            self.editCell($(this));
        });

        // Formula bar input
        $("#formula-input").on("keypress", function (e) {
            if (e.which === 13 && self.selectedCell) {
                const value = $(this).val();
                self.setCellValue(self.selectedCell, value);
                self.selectedCell.removeClass("editing");
                $(this).blur();
            }
        });

        $("#formula-input").on("input", function () {
            if (self.selectedCell) {
                const value = $(this).val();
                self.selectedCell
                    .find(".cell-display")
                    .text(self.formatCellValue(value));
            }
        });

        // Keyboard navigation and shortcuts
        $(document).on("keydown", function (e) {
            // Check if we should handle keyboard shortcuts
            const shouldHandleShortcuts = self.selectedCell &&
                !$("#formula-input").is(":focus") &&
                !self.isInModalOrInput(e.target);

            if (shouldHandleShortcuts) {
                // Handle copy/paste shortcuts (CMD+C, CMD+V on Mac; Ctrl+C, Ctrl+V on Windows)
                if ((e.metaKey || e.ctrlKey) && e.key === 'c') {
                    e.preventDefault();
                    self.copyCell();
                    return;
                }

                if ((e.metaKey || e.ctrlKey) && e.key === 'v') {
                    e.preventDefault();
                    self.pasteCell();
                    return;
                }

                // Handle backspace to clear cells
                if (e.key === 'Backspace') {
                    e.preventDefault();
                    self.clearCell();
                    return;
                }

                // Handle Escape to cancel cell editing
                if (e.key === 'Escape') {
                    e.preventDefault();
                    if (self.selectedCell && self.selectedCell.hasClass('editing')) {
                        self.cancelCellEditing(self.selectedCell);
                    }
                    return;
                }

                // Handle other key navigation
                self.handleKeyNavigation(e);
            }
        });

        // Toolbar buttons
        $("#clear-btn").click(() => self.clearCell());

        $("#manage-tournaments").click(() => self.showTournamentsModal());
        $("#add-user-btn").click(() => self.showAddUserModal());
        $("#add-new-tournament").click(() => self.showTournamentFormModal());

        $("#year-selector").change(function () {
            self.switchYear(parseInt($(this).val()));
        });

        $("#tournament-filter-btn").click(() => self.showTournamentFilterModal());

        $("#assigned-users-toggle").click(() => self.toggleAssignedUsersFilter());

        $("#today-btn").click(() => self.goToToday());

        // Report button and modal events
        $("#report-btn").click(() => self.openReportModal());
        //$("#export-excel-btn").click(() => {
        //    window.location = ProcessUrl('Tournament/ExportEventSchedule?year=' + self.year);
        //});
        $("#report-select-all").click(() => {
            $('.report-tournament-checkbox').prop('checked', true);
            self.updateStaffSelectionForReport();
        });
        $("#report-deselect-all").click(() => {
            $('.report-tournament-checkbox').prop('checked', false);
            self.updateStaffSelectionForReport();
        });
        // Update staff list when tournament selection changes
        $(document).on('change', '.report-tournament-checkbox', () => {
            self.updateStaffSelectionForReport();
        });
        // Staff select/deselect all
        $("#report-staff-select-all").click(() => {
            $('.report-staff-checkbox').prop('checked', true);
        });
        $("#report-staff-deselect-all").click(() => {
            $('.report-staff-checkbox').prop('checked', false);
        });
        $("#generate-report-btn").click(() => self.generateReport());

        // Tournament modal events
        $("#tournament-form").submit((e) => {
            e.preventDefault();
            self.saveTournament();
        });

        // Add User modal events
        $("#add-user-form").submit((e) => {
            e.preventDefault();
            self.saveUser();
        });

        $("#save-user").click(() => {
            $("#add-user-form").submit();
        });

        // Auto-calculate phase dates when tournament dates change
        // DISABLED: Phase dates are now manually editable without auto-update
        // $("#tournament-start, #tournament-end").on("change", function () {
        //     self.calculatePhaseDates();
        // });

        // Context menu
        $(document).on("contextmenu", ".cell", function (e) {
            e.preventDefault();

            const clickedCell = $(this);
            const clickedCellId = clickedCell.data("cell");

            // Check if right-clicked cell is in current selection
            const isInSelection = self.selectedCells.some(cell =>
                cell.data("cell") === clickedCellId
            );

            // If right-clicked cell is not in current selection, select it
            if (!isInSelection) {
                self.selectCell(clickedCell);
            }

            self.showContextMenu(e.pageX, e.pageY);
        });

        // Column header context menu
        $(document).on("contextmenu", ".col-header", function (e) {
            e.preventDefault();
            self.selectedColumn = $(this);
            self.showColumnContextMenu(e.pageX, e.pageY);
        });

        $(document).on("click", ".context-menu-item", function () {
            const action = $(this).data("action");
            self.handleContextAction(action);
            self.hideContextMenu();
        });

        $(document).click(() => self.hideContextMenu());

        // Click outside to deselect
        $(document).on("click", function (e) {
            // Don't deselect if we just finished a drag operation
            if (self.justFinishedDragging) {
                return;
            }

            if (
                !$(e.target).closest(
                    ".cell, .formula-bar, .toolbar, .modal, #context-menu"
                ).length
            ) {
                self.deselectCell();
            }
        });
    }

    selectCell(cell) {
        // Clear previous selections
        this.clearAllSelections();
        if (this.selectedCell) {
            this.finishCellEditing(this.selectedCell);
            this.selectedCell.removeClass("selected");
        }

        this.selectedCell = cell;
        this.selectedCells = [cell];
        cell.addClass("selected");

        const rowIndex = parseInt(cell.data("row"));

        // Highlight the associated date cell
        this.highlightDateCells([rowIndex]);
        const col = parseInt(cell.data("col"));
        const columnName = this.columnNames[col];
        const tournamentId = cell.data("tournament");
        const phase = cell.data("phase");

        if (rowIndex !== undefined && this.scheduleRows[rowIndex]) {
            const rowData = this.scheduleRows[rowIndex];
            const dateString = rowData.date.toLocaleDateString();
            if (rowData.tournament) {
                const reference = `${columnName} - ${rowData.tournament.name} (${phase ? phase.toUpperCase() : "UNKNOWN"
                    }) - ${dateString}`;
                this.updateCellReference(reference);
            } else {
                const reference = `${columnName} - ${dateString}`;
                this.updateCellReference(reference);
            }
        } else {
            this.updateCellReference(columnName + " - Unknown Date");
        }

        const cellId = `${col}_${rowIndex}`;
        const cellData = this.data[cellId];
        const value = cellData ? cellData.originalValue || cellData.value : "";
        $("#formula-input").val(value);

        // Update selected cell count
        this.updateSelectedCellCount();
    }

    // Update the selected cell count display in the status bar
    updateSelectedCellCount() {
        const count = this.selectedCells.length;
        const countDisplay = $('#selected-cell-count');
        if (count > 0) {
            countDisplay.text(`${count} cell${count > 1 ? 's' : ''} selected`);
        } else {
            countDisplay.text('');
        }
    }

    finishCellEditing(cell) {
        if (cell && cell.hasClass("editing")) {
            const input = cell.find("input.cell-input");
            if (input.length) {
                // Save the current input value
                this.setCellValue(cell, input.val());
                cell.removeClass("editing");
            }
        }
    }

    cancelCellEditing(cell) {
        if (cell && cell.hasClass("editing")) {
            // Restore original content without saving changes
            cell.removeClass("editing");
            const rowIndex = parseInt(cell.data("row"));
            const colIndex = parseInt(cell.data("col"));
            const cellId = `${colIndex}_${rowIndex}`;
            const cellData = this.data[cellId];
            const displayValue = this.formatCellValue(cellData?.value || "");
            cell.html(`<span class="cell-display">${displayValue}</span>`);

            // Apply any existing cell styling and content
            this.applyCellStyling(cell, cellData);
        }
    }

    applyCellStyling(cell, cellData) {
        if (!cellData) return;

        // Remove existing state classes
        cell.removeClass("assigned not-working travel");

        // Remove existing role classes
        cell.removeClass((index, className) => {
            return (className.match(/\brole-\S+/g) || []).join(' ');
        });

        // Remove existing phase classes
        cell.removeClass((index, className) => {
            return (className.match(/\bphase-\S+/g) || []).join(' ');
        });

        // Apply tournament assignment states
        if (cellData.assigned) {
            cell.addClass("assigned");
        }
        if (cellData.notWorking) {
            cell.addClass("not-working");
        }
        if (cellData.travel) {
            cell.addClass("travel");
        }

        // Apply role class if it exists
        if (cellData.role) {
            const roleClass = `role-${cellData.role.toLowerCase().replace(/\s+/g, "-")}`;
            cell.addClass(roleClass);
        }

        // Apply phase class if assigned
        if (cellData.phase && cellData.assigned) {
            cell.addClass(`phase-${cellData.phase}`);
        }

        // Apply tournament colors for assigned cells
        if (cellData.tournament && cellData.assigned) {
            const tournamentColorMap = this.getTournamentColorMap();
            const tournamentColor = tournamentColorMap.get(cellData.tournament);

            if (tournamentColor) {
                cell.css('background', tournamentColor);
            }
        }

        // Apply phase border color as inline style (overrides other borders)
        if (cellData.phase && cellData.assigned) {
            const phaseBorderColors = {
                setup: '#000000',
                advanced: '#334155',
                tournament: '#FFFFFF',
                teardown: '#6B7280'
            };
            const phaseBorderColor = phaseBorderColors[cellData.phase];
            if (phaseBorderColor) {
                const existingStyle = cell.attr("style") || "";
                cell.attr("style", existingStyle + ` border-left: 4px solid ${phaseBorderColor} !important;`);
            }
        }

        // Handle cell content for special states
        if (cellData.assigned && cellData.tournament) {
            const tournament = this.tournaments.find(t => t.id == cellData.tournament);
            if (tournament) {
                const phaseDisplayNames = {
                    setup: "Setup",
                    advanced: "Advanced",
                    tournament: "Event",
                    teardown: "Takedown",
                };

                const cellContent = `
          <div class="cell-content-wrapper">
            <div class="cell-main-content">
              <div class="tournament-info">${tournament.name}</div>
              <div class="assignment-status">${phaseDisplayNames[cellData.phase] || cellData.phase}</div>
            </div>
            ${cellData.role ? `<div class="role-badge">${this.getRoleAbbreviation(cellData.role)}</div>` : ''}
          </div>
        `;
                cell.find(".cell-display").html(cellContent);
            }
        } else if (cellData.notWorking) {
            const tournament = this.tournaments.find(t => t.id == cellData.tournament);
            if (tournament) {
                const cellContent = `
          <div class="tournament-info">${tournament.name}</div>
          <div class="assignment-status">Not Working</div>
        `;
                cell.find(".cell-display").html(cellContent);
            }
        } else if (cellData.travel) {
            const cellContent = `
        <div class="assignment-status">Travel</div>
      `;
            cell.find(".cell-display").html(cellContent);
        }
    }

    editCell(cell) {
        const rowIndex = parseInt(cell.data("row"));
        const colIndex = parseInt(cell.data("col"));
        const cellId = `${colIndex}_${rowIndex}`;
        const cellData = this.data[cellId];
        const currentValue = cellData?.originalValue || cellData?.value || "";

        cell.addClass("editing");
        cell.html(`<input type="text" value="${currentValue}" class="cell-input">`);

        const input = cell.find("input");
        input.focus().select();

        input.on("keypress", (e) => {
            if (e.which === 13) {
                this.setCellValue(cell, input.val());
                cell.removeClass("editing");
            }
        });

        input.on("blur", () => {
            this.setCellValue(cell, input.val());
            cell.removeClass("editing");
        });
    }

    setCellValue(cell, value) {
        const rowIndex = parseInt(cell.data("row"));
        const colIndex = parseInt(cell.data("col"));


        const cellId = `${colIndex}_${rowIndex}`;
        if (!this.data[cellId]) {
            this.data[cellId] = { value: "", originalValue: "", style: {} };
        }

        this.data[cellId].originalValue = value;

        this.data[cellId].value = value;

        // Update display
        const displayValue = this.formatCellValue(value);
        cell.html(`<span class="cell-display">${displayValue}</span>`);

        // Update formula bar
        $("#formula-input").val(value);

        this.saveToStorage();
    }

    // Remove unused calendar methods that are causing errors
    isLeapYear(year) {
        return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
    }

    getDayOfYear(date = new Date()) {
        const start = new Date(date.getFullYear(), 0, 1);
        const diff = date - start;
        const oneDay = 1000 * 60 * 60 * 24;
        return Math.floor(diff / oneDay) + 1;
    }

    async switchYear(newYear) {
        if (newYear < 1900 || newYear > 2100) return;

        // Save current year's data before switching
        this.saveToStorage();

        // Switch to new year
        this.year = newYear;

        // Update the year display in the title
        const yearDisplay = document.getElementById('current-year-display');
        if (yearDisplay) {
            yearDisplay.textContent = newYear;
        }

        // Clear all existing data before loading new year
        this.data = {};
        this.tournaments = [];
        this.scheduleRows = [];
        this.allScheduleRows = [];

        // Clear tournament filter when switching years
        this.filteredTournamentIds.clear();
        this.updateFilterButtonText();

        // Show loading indicator
        if (window.showLoading) {
            window.showLoading();
        }

        try {
            // Load from database
            await this.loadTournamentsFromDatabase();

            // Reload columns to filter by termination date for the new year
            await this.loadColumnsFromDatabase();

            // Generate schedule rows FIRST so loadScheduleData can find the dates
            this.generateScheduleRows();
            this.applyTournamentFilter();

            // Then load schedule assignments
            await this.loadScheduleData();

            // Recalculate assigned users if filter is active
            if (this.showOnlyAssignedUsers) {
                this.assignedUserColumns = this.getAssignedUserColumns();
            }

            // Re-render the entire spreadsheet
            this.createGrid();

            // Apply loaded assignments to the visual grid
            if (this.applyLoadedAssignmentsToGrid) {
                this.applyLoadedAssignmentsToGrid();
            }

            // Re-sort columns by assignment count for new year
            this.sortColumnsByAssignmentCount();

            // Update the year selector
            this.populateYearSelector();

            console.log(`Successfully switched to year ${newYear}`);

        } catch (error) {
            console.error('Error loading data for year ' + newYear + ':', error);
            if (window.showError) {
                window.showError(`Failed to load schedule data for ${newYear}: ${error.message}`);
            }

            // Fall back to loading from local storage
            this.loadFromStorage();
            this.populateYearSelector();
        } finally {
            // Hide loading indicator
            if (window.hideLoading) {
                window.hideLoading();
            }
        }
    }

    formatTournamentDate(rowData) {
        const date = rowData.date;
        const shortDate = `${date.getMonth() + 1}/${date.getDate()}`;

        if (rowData.allTournaments && rowData.allTournaments.length > 1) {
            // Multiple tournaments - show just the date with overlap indicator
            return `⚡\n${shortDate}`;
        } else if (rowData.tournament && rowData.phase) {
            // Single tournament day - show phase info
            const phasePrefix = {
                setup: "S",
                advanced: "A",
                tournament: "E",
                teardown: "T",
            }[rowData.phase];

            return `${phasePrefix}${rowData.dayInPhase}\n${shortDate}`;
        } else {
            // Regular day
            return shortDate;
        }
    }

    showTournamentFilterModal() {
        const modal = new bootstrap.Modal("#tournamentFilterModal");
        this.populateTournamentFilterList();
        modal.show();
    }

    populateTournamentFilterList() {
        const listContainer = $("#tournament-filter-list");
        listContainer.empty();

        // Filter tournaments to only show those with any activity in the current year (including phase dates)
        const currentYearTournaments = this.tournaments.filter(tournament =>
            this.isTournamentInYear(tournament, this.year)
        );

        if (currentYearTournaments.length === 0) {
            listContainer.append('<p class="text-muted">No tournaments available</p>');
            return;
        }

        currentYearTournaments.forEach((tournament) => {
            const isChecked = this.filteredTournamentIds.size === 0 || this.filteredTournamentIds.has(tournament.id);

            const checkboxItem = $(`
        <div class="form-check mb-2">
          <input class="form-check-input tournament-filter-checkbox" type="checkbox"
                 value="${tournament.id}" id="tournament-filter-${tournament.id}"
                 ${isChecked ? 'checked' : ''}>
          <label class="form-check-label" for="tournament-filter-${tournament.id}">
            <strong>${tournament.name}</strong><br>
            <small class="text-muted">${tournament.location} • ${tournament.startDate.toLocaleDateString()} - ${tournament.endDate.toLocaleDateString()}</small>
          </label>
        </div>
      `);

            listContainer.append(checkboxItem);
        });

        // Add event handlers for the modal buttons
        $("#select-all-tournaments").off('click').on('click', () => {
            $('.tournament-filter-checkbox').prop('checked', true);
        });

        $("#deselect-all-tournaments").off('click').on('click', () => {
            $('.tournament-filter-checkbox').prop('checked', false);
        });

        $("#apply-tournament-filter").off('click').on('click', () => {
            this.applyTournamentFilterFromModal();
        });
    }

    applyTournamentFilterFromModal() {
        const checkedBoxes = $('.tournament-filter-checkbox:checked');

        // Get current year tournaments count for comparison (including phase dates)
        const currentYearTournaments = this.tournaments.filter(tournament =>
            this.isTournamentInYear(tournament, this.year)
        );

        if (checkedBoxes.length === 0) {
            // No tournaments selected, clear filter (show all)
            this.filteredTournamentIds.clear();
        } else if (checkedBoxes.length === currentYearTournaments.length) {
            // All current year tournaments selected, clear filter (show all)
            this.filteredTournamentIds.clear();
        } else {
            // Specific tournaments selected
            this.filteredTournamentIds.clear();
            checkedBoxes.each((index, checkbox) => {
                this.filteredTournamentIds.add(parseInt($(checkbox).val()));
            });
        }

        // Update the button text
        this.updateFilterButtonText();

        // Regenerate the schedule with the new filter
        this.generateScheduleRows();
        this.applyTournamentFilter();

        // NOTE: "Assigned Only" toggle with tournament filter is currently disabled
        // Issue: Virtual scrolling doesn't properly re-render when column visibility changes
        // The second createGrid() call doesn't trigger _renderVisibleRows() execution
        // TODO: Fix virtual scrolling to handle column visibility changes, or refactor to avoid double rendering

        // Re-sort columns based on filtered tournament assignments
        this.sortColumnsByAssignmentCount();

        // COMMENTED OUT - Assigned Only integration
        // const wasShowingOnlyAssigned = this.showOnlyAssignedUsers;
        // if (wasShowingOnlyAssigned) {
        //     this.assignedUserColumns.clear();
        //     this.sortColumnsByAssignmentCount();
        //     this.assignedUserColumns = this.getAssignedUserColumns();
        //     this.virtualScrolling.renderedRange = { start: -1, end: -1 };
        //     this.createGrid();
        // }

        // Close the modal
        const modal = bootstrap.Modal.getInstance("#tournamentFilterModal");
        modal.hide();
    }

    updateFilterButtonText() {
        const button = $("#tournament-filter-btn");

        if (this.filteredTournamentIds.size === 0) {
            button.text("🔍 Filter: All Tournaments");
        } else if (this.filteredTournamentIds.size === 1) {
            const tournamentId = Array.from(this.filteredTournamentIds)[0];
            const tournament = this.tournaments.find(t => t.id === tournamentId);
            button.text(`🔍 Filter: ${tournament ? tournament.name : '1 Tournament'}`);
        } else {
            button.text(`🔍 Filter: ${this.filteredTournamentIds.size} Tournaments`);
        }
    }

    showTournamentsModal() {
        const modal = new bootstrap.Modal("#tournamentsModal");
        $("#tournaments-year").text(this.year);
        this.populateTournamentsList();
        modal.show();
    }

    showTournamentFormModal(tournament = null) {
        const modal = new bootstrap.Modal("#tournamentFormModal");

        // Initialize Select2 for client dropdown with search
        // Destroy existing Select2 instance if it exists
        if ($('#tournament-client').hasClass('select2-hidden-accessible')) {
            $('#tournament-client').select2('destroy');
        }

        //console.log('Initializing Select2 for client dropdown');

        $('#tournament-client').select2({
            ajax: {
                url: ProcessUrl('Tournament/GetClients'),
                dataType: 'json',
                delay: 250,
                data: function (params) {
                    //console.log('Select2 search term:', params.term);
                    return {
                        search: params.term || '' // Send search term to backend
                    };
                },
                processResults: function (data) {
                    console.log('Select2 received data:', data);
                    return {
                        results: data.map(c => ({ id: c.Id, text: c.Title }))
                    };
                },
                cache: true
            },
            placeholder: '-- Select Client --',
            allowClear: true,
            width: '100%',
            minimumInputLength: 3, // Require 3 characters minimum
            dropdownParent: $('#tournamentFormModal'),
            language: {
                inputTooShort: function(args) {
                    var remainingChars = args.minimum - args.input.length;
                    return 'Please enter ' + remainingChars + ' or more characters';
                },
                searching: function() {
                    return "Searching...";
                },
                noResults: function() {
                    return "No clients found";
                }
            }
        });

        //console.log('Select2 initialized');

        if (tournament) {
            $("#tournamentFormModalLabel").text("Edit Tournament");
            $("#tournament-name").val(tournament.name);
            $("#tournament-location").val(tournament.location);
            $("#tournament-start").val(
                tournament.startDate.toISOString().split("T")[0]
            );
            $("#tournament-end").val(tournament.endDate.toISOString().split("T")[0]);

            // Populate phase dates if they exist, otherwise calculate defaults
            if (tournament.setupStart) {
                // Handle both Date objects and string dates
                const setupStart = tournament.setupStart instanceof Date ? tournament.setupStart : new Date(tournament.setupStart);
                const setupEnd = tournament.setupEnd instanceof Date ? tournament.setupEnd : new Date(tournament.setupEnd);
                const advancedStart = tournament.advancedStart instanceof Date ? tournament.advancedStart : new Date(tournament.advancedStart);
                const advancedEnd = tournament.advancedEnd instanceof Date ? tournament.advancedEnd : new Date(tournament.advancedEnd);
                const teardownStart = tournament.teardownStart instanceof Date ? tournament.teardownStart : new Date(tournament.teardownStart);
                const teardownEnd = tournament.teardownEnd instanceof Date ? tournament.teardownEnd : new Date(tournament.teardownEnd);

                $("#setup-start").val(setupStart.toISOString().split("T")[0]);
                $("#setup-end").val(setupEnd.toISOString().split("T")[0]);
                $("#advanced-start").val(advancedStart.toISOString().split("T")[0]);
                $("#advanced-end").val(advancedEnd.toISOString().split("T")[0]);
                $("#teardown-start").val(teardownStart.toISOString().split("T")[0]);
                $("#teardown-end").val(teardownEnd.toISOString().split("T")[0]);
            } else {
                this.calculatePhaseDates();
            }

            // Populate new fields
            $("#minimum-wage").val(tournament.minimumWage || 0);
            $("#per-diem").val(tournament.perDiem || 0);
            $("#travel-per-diem").val(tournament.travelPerDiem || 0);
            $("#bill-rate").val(tournament.billRate || '');
            $("#manager-bill-rate").val(tournament.managerBillRate || '');
            $("#print-bill-rate").val(tournament.printBillRate || '');
            $("#ot-calculation").val(tournament.otByDay ? 'daily' : 'weekly');

            // Populate client dropdown for edit mode
            if (tournament.clientId && tournament.clientName) {
                const option = new Option(tournament.clientName, tournament.clientId, true, true);
                $('#tournament-client').append(option).trigger('change');
            }

            $("#save-tournament").data("tournament-id", tournament.id);
        } else {
            $("#tournamentFormModalLabel").text("Add Tournament");
            $("#tournament-form")[0].reset();
            $('#tournament-client').val(null).trigger('change');
            $("#save-tournament").removeData("tournament-id");
        }
        modal.show();
    }

    calculatePhaseDates() {
        const startDate = $("#tournament-start").val();
        const endDate = $("#tournament-end").val();

        if (!startDate || !endDate) return;

        const tournamentStart = new Date(startDate);
        const tournamentEnd = new Date(endDate);

        // Calculate Setup (21 days before tournament, lasting 14 days)
        const setupStart = new Date(tournamentStart);
        setupStart.setDate(setupStart.getDate() - 21);
        const setupEnd = new Date(setupStart);
        setupEnd.setDate(setupEnd.getDate() + 13); // +13 to get 14 days total (inclusive)

        // Calculate Advanced Week (starts day after setup ends, lasting 7 days)
        const advancedStart = new Date(setupEnd);
        advancedStart.setDate(advancedStart.getDate() + 1);
        const advancedEnd = new Date(tournamentStart);
        advancedEnd.setDate(advancedEnd.getDate() - 1);

        // Calculate Teardown (6 days after tournament)
        const teardownStart = new Date(tournamentEnd);
        teardownStart.setDate(teardownStart.getDate() + 1);
        const teardownEnd = new Date(tournamentEnd);
        teardownEnd.setDate(teardownEnd.getDate() + 6);

        // Populate the form fields
        $("#setup-start").val(setupStart.toISOString().split("T")[0]);
        $("#setup-end").val(setupEnd.toISOString().split("T")[0]);
        $("#advanced-start").val(advancedStart.toISOString().split("T")[0]);
        $("#advanced-end").val(advancedEnd.toISOString().split("T")[0]);
        $("#teardown-start").val(teardownStart.toISOString().split("T")[0]);
        $("#teardown-end").val(teardownEnd.toISOString().split("T")[0]);
    }

    populateTournamentsList() {
        const listContainer = $("#tournaments-list");
        const noTournaments = $("#no-tournaments");

        // Filter tournaments to only show those with any activity in the current year (including phase dates)
        const currentYearTournaments = this.tournaments.filter(tournament =>
            this.isTournamentInYear(tournament, this.year)
        );

        if (currentYearTournaments.length === 0) {
            noTournaments.show();
            listContainer.find(".tournament-item").remove();
            return;
        }

        noTournaments.hide();
        listContainer.find(".tournament-item").remove();

        currentYearTournaments.forEach((tournament) => {
            // Calculate full date range from setup to teardown
            const setupStart = tournament.setupStart ?
                (tournament.setupStart instanceof Date ? tournament.setupStart : new Date(tournament.setupStart)) :
                (() => {
                    const date = new Date(tournament.startDate);
                    date.setDate(date.getDate() - 21);
                    return date;
                })();

            const teardownEnd = tournament.teardownEnd ?
                (tournament.teardownEnd instanceof Date ? tournament.teardownEnd : new Date(tournament.teardownEnd)) :
                (() => {
                    const date = new Date(tournament.endDate);
                    date.setDate(date.getDate() + 6);
                    return date;
                })();

            const tournamentItem = $(`
                        <div class="tournament-item border rounded p-2 mb-2">
                            <div class="d-flex justify-content-between align-items-start">
                                <div class="flex-grow-1">
                                    <h6 class="mb-1">${tournament.name}</h6>
                                    <div class="text-muted small">
                                        <div class="row">
                                            <div class="col-sm-6">
                                                <div>📍 ${tournament.location}</div>
                                                <div><strong>Event:</strong> ${tournament.startDate.toLocaleDateString()} - ${tournament.endDate.toLocaleDateString()}</div>
                                            </div>
                                            <div class="col-sm-6">
                                                <div><strong>Full Range:</strong> ${setupStart.toLocaleDateString()} - ${teardownEnd.toLocaleDateString()}</div>
                                                <div class="text-primary"><small>Setup → Event → Teardown</small></div>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                                <div class="btn-group btn-group-sm">
                                    <button type="button" class="btn btn-outline-primary edit-tournament" data-id="${tournament.id
                }">
                                        Edit
                                    </button>
                                    <button type="button" class="btn btn-outline-danger delete-tournament" data-id="${tournament.id
                }">
                                        Delete
                                    </button>
                                </div>
                            </div>
                        </div>
                    `);
            listContainer.append(tournamentItem);
        });

        // Add event listeners for edit and delete buttons
        $(".edit-tournament").click((e) => {
            const tournamentId = parseInt($(e.target).data("id"));
            const tournament = this.tournaments.find((t) => t.id === tournamentId);
            if (tournament) {
                this.showTournamentFormModal(tournament);
            }
        });

        $(".delete-tournament").click((e) => {
            const tournamentId = parseInt($(e.target).data("id"));
            this.deleteTournament(tournamentId);
        });
    }

    async deleteTournament(tournamentId) {
        if (
            confirm(
                "Are you sure you want to delete this tournament? This will also remove all associated schedule data."
            )
        ) {
            try {
                // Delete from backend first - call API directly
                console.log('Deleting tournament from backend...', tournamentId);

                const response = await fetch(ProcessUrl('Tournament/Delete/' + tournamentId), {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    }
                });

                if (!response.ok) {
                    throw new Error(`HTTP error! status: ${response.status}`);
                }

                const result = await response.json();
                if (!result.success) {
                    throw new Error(result.message || 'Failed to delete tournament');
                }

                console.log('Tournament deleted successfully from backend');

                // Reload tournaments from database to get fresh data
                await this.loadTournamentsFromDatabase();
                this.generateScheduleRows();
                this.applyTournamentFilter();
                this.createGrid();
                this.populateTournamentsList();

                this.showUserFeedback('Tournament deleted successfully', 'success');
            } catch (error) {
                console.error('Error deleting tournament:', error);
                this.showUserFeedback('Failed to delete tournament', 'error');
            }
        }
    }

    async saveTournament() {
        const name = $("#tournament-name").val();
        const location = $("#tournament-location").val();

        // Step 1: Get raw input values
        const startDateInput = $("#tournament-start").val();
        const endDateInput = $("#tournament-end").val();

        // Helper function to create Date from YYYY-MM-DD string in LOCAL timezone (not UTC)
        const createLocalDate = (dateString) => {
            if (!dateString) return null;
            const [year, month, day] = dateString.split('-').map(Number);
            return new Date(year, month - 1, day); // month is 0-indexed
        };

        // Step 2: Create Date objects using LOCAL timezone (NOT UTC)
        const startDate = createLocalDate(startDateInput);
        const endDate = createLocalDate(endDateInput);

        // Get phase dates (using LOCAL timezone)
        const setupStartInput = $("#setup-start").val();
        const setupStart = createLocalDate(setupStartInput);
        const setupEnd = createLocalDate($("#setup-end").val());
        const advancedStart = createLocalDate($("#advanced-start").val());
        const advancedEnd = createLocalDate($("#advanced-end").val());
        const teardownStart = createLocalDate($("#teardown-start").val());
        const teardownEnd = createLocalDate($("#teardown-end").val());

        // Get new fields
        const minimumWage = parseFloat($("#minimum-wage").val()) || 0;
        const perDiem = parseFloat($("#per-diem").val()) || 0;
        const travelPerDiem = parseFloat($("#travel-per-diem").val()) || 0;
        const billRate = parseFloat($("#bill-rate").val()) || null;
        const managerBillRate = parseFloat($("#manager-bill-rate").val()) || null;
        const printBillRate = parseFloat($("#print-bill-rate").val()) || null;
        const otCalculation = $("#ot-calculation").val();
        const otByDay = otCalculation === 'daily';

        // Get client ID from Select2 dropdown
        const clientId = $('#tournament-client').val();

        if (!name || !location || !startDate || !endDate) {
            alert("Please fill in all fields");
            return;
        }

        if (!clientId) {
            alert("Please select a client");
            return;
        }

        if (startDate >= endDate) {
            alert("End date must be after start date");
            return;
        }

        // Validate phase dates
        if (setupStart > setupEnd || advancedStart > advancedEnd || teardownStart > teardownEnd) {
            alert("Phase start dates must be before their end dates");
            return;
        }

        const tournamentId = $("#save-tournament").data("tournament-id");

        if (tournamentId) {
            // Edit existing tournament
            const tournament = this.tournaments.find((t) => t.id === tournamentId);
            if (tournament) {
                tournament.name = name;
                tournament.location = location;
                tournament.startDate = startDate;
                tournament.endDate = endDate;
                tournament.setupStart = setupStart;
                tournament.setupEnd = setupEnd;
                tournament.advancedStart = advancedStart;
                tournament.advancedEnd = advancedEnd;
                tournament.teardownStart = teardownStart;
                tournament.teardownEnd = teardownEnd;
                tournament.minimumWage = minimumWage;
                tournament.perDiem = perDiem;
                tournament.travelPerDiem = travelPerDiem;
                tournament.billRate = billRate;
                tournament.managerBillRate = managerBillRate;
                tournament.printBillRate = printBillRate;
                tournament.otByDay = otByDay;
                tournament.clientId = parseInt(clientId);
            }
        } else {
            // Add new tournament
            this.tournaments.push({
                id: this.nextTournamentId++,
                name: name,
                location: location,
                startDate: startDate,
                endDate: endDate,
                setupStart: setupStart,
                setupEnd: setupEnd,
                advancedStart: advancedStart,
                advancedEnd: advancedEnd,
                teardownStart: teardownStart,
                teardownEnd: teardownEnd,
                minimumWage: minimumWage,
                perDiem: perDiem,
                travelPerDiem: travelPerDiem,
                billRate: billRate,
                managerBillRate: managerBillRate,
                printBillRate: printBillRate,
                otByDay: otByDay,
                clientId: parseInt(clientId),
            });
        }

        this.generateScheduleRows();
        this.createGrid();

        // Save tournament to database
        try {
            if (tournamentId) {
                // Update existing tournament
                const tournament = this.tournaments.find(t => t.id === tournamentId);
                if (tournament) {
                    await this.updateTournamentDirectly(tournament);
                }
            } else {
                // Create new tournament
                const newTournament = this.tournaments[this.tournaments.length - 1]; // Latest added
                await this.createTournamentDirectly(newTournament);
            }
        } catch (error) {
            console.error('Error saving tournament:', error);
            this.showUserFeedback('Failed to save tournament: ' + error.message, 'error');
            return;
        }

        // Close the form modal
        const formModal = bootstrap.Modal.getInstance("#tournamentFormModal");
        if (formModal) {
            formModal.hide();
        }

        // Refresh the tournaments list if the main modal is open
        if (bootstrap.Modal.getInstance("#tournamentsModal")) {
            this.populateTournamentsList();
        }
    }

    async saveTournamentToDatabase(action) {
        try {
            const tournaments = this.tournaments.map(t => ({
                Id: t.id,
                Name: t.name,
                Location: t.location,
                StartDate: t.startDate.toISOString(),
                EndDate: t.endDate.toISOString(),
                SetupStartDate: t.setupStart ? t.setupStart.toISOString() : null,
                SetupEndDate: t.setupEnd ? t.setupEnd.toISOString() : null,
                AdvanceWeekStartDate: t.advancedStart ? t.advancedStart.toISOString() : null,
                AdvanceWeekEndDate: t.advancedEnd ? t.advancedEnd.toISOString() : null,
                TeardownStartDate: t.teardownStart ? t.teardownStart.toISOString() : null,
                TeardownEndDate: t.teardownEnd ? t.teardownEnd.toISOString() : null,
                Status: 1, // Active status
                ClientId: 1, // Default client - should be made configurable
                MinimumWage: t.minimumWage || 0,
                PerDiem: t.perDiem || 0,
                OTByDay: t.otByDay || false
            }));

            console.log(`${action} tournament - sending to database:`, tournaments);

            const response = await fetch(ProcessUrl('Tournament/SaveTournaments'), {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify(tournaments)
            });

            if (response.ok) {
                const result = await response.json();
                console.log('Tournament save successful:', result);
                this.showUserFeedback(`Tournament ${action}d successfully!`, 'success');
            } else {
                console.error('Tournament save failed:', response.status, response.statusText);
                this.showUserFeedback(`Failed to ${action} tournament. Please try again.`, 'warning');
            }
        } catch (error) {
            console.error('Error saving tournament to database:', error);
            this.showUserFeedback(`Error saving tournament: ${error.message}`, 'warning');
        }
    }

    manageTournaments() {
        let html = `<div style="padding: 20px;"><h3>Manage Tournaments (${this.year})</h3>`;

        // Filter tournaments to only show those with any activity in the current year (including phase dates)
        const currentYearTournaments = this.tournaments.filter(tournament =>
            this.isTournamentInYear(tournament, this.year)
        );

        if (currentYearTournaments.length === 0) {
            html += '<p style="color: #666;">No tournaments found for this year.</p>';
        } else {
            currentYearTournaments.forEach((tournament) => {
                html += `
                        <div style="border: 1px solid #ddd; padding: 10px; margin: 10px 0; border-radius: 5px;">
                            <strong>${tournament.name}</strong><br>
                            Location: ${tournament.location}<br>
                            Dates: ${tournament.startDate.toLocaleDateString()} - ${tournament.endDate.toLocaleDateString()}<br>
                            <button onclick="spreadsheetApp.editTournament(${tournament.id
                    })" style="margin: 5px;">Edit</button>
                            <button onclick="spreadsheetApp.deleteTournament(${tournament.id
                    })" style="margin: 5px;">Delete</button>
                        </div>
                    `;
            });
        }

        html += "</div>";

        const popup = window.open(
            "",
            "_blank",
            "width=600,height=400,scrollbars=yes"
        );
        popup.document.write(html);
        popup.document.close();
    }

    editTournament(tournamentId) {
        const tournament = this.tournaments.find((t) => t.id === tournamentId);
        if (tournament) {
            // this.showTournamentModal(tournament);
            this.showTournamentFormModal(tournament);
        }
    }

    async deleteTournament(tournamentId) {
        if (confirm("Are you sure you want to delete this tournament?")) {
            try {
                // Delete from backend first - call API directly
                console.log('Deleting tournament from backend...', tournamentId);

                const response = await fetch(ProcessUrl('Tournament/Delete/' + tournamentId), {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    }
                });

                if (!response.ok) {
                    throw new Error(`HTTP error! status: ${response.status}`);
                }

                const result = await response.json();
                if (!result.success) {
                    throw new Error(result.message || 'Failed to delete tournament');
                }

                console.log('Tournament deleted successfully from backend');

                // Reload tournaments from database to get fresh data
                await this.loadTournamentsFromDatabase();
                this.generateScheduleRows();
                this.applyTournamentFilter();
                this.createGrid();

                this.showUserFeedback('Tournament deleted successfully', 'success');
            } catch (error) {
                console.error('Error deleting tournament:', error);
                this.showUserFeedback('Failed to delete tournament', 'error');
            }
        }
    }

    async updateTournamentDirectly(tournament) {
        console.log('Updating tournament in backend...', tournament);

        // Helper function to format date without timezone conversion
        const formatDateOnly = (date) => {
            if (!date) return null;
            const d = date instanceof Date ? date : new Date(date);
            const year = d.getFullYear();
            const month = String(d.getMonth() + 1).padStart(2, '0');
            const day = String(d.getDate()).padStart(2, '0');
            return `${year}-${month}-${day}`;
        };


        const startDateFormatted = formatDateOnly(tournament.startDate);
        const endDateFormatted = formatDateOnly(tournament.endDate);

        const payload = {
            Id: tournament.id,
            Name: tournament.name,
            Description: tournament.name + ' Tournament',
            Location: tournament.location,
            StartDate: startDateFormatted,
            EndDate: endDateFormatted,
            SetupStartDate: formatDateOnly(tournament.setupStart),
            SetupEndDate: formatDateOnly(tournament.setupEnd),
            AdvanceWeekStartDate: formatDateOnly(tournament.advancedStart),
            AdvanceWeekEndDate: formatDateOnly(tournament.advancedEnd),
            TeardownStartDate: formatDateOnly(tournament.teardownStart),
            TeardownEndDate: formatDateOnly(tournament.teardownEnd),
            Status: tournament.status || 1,
            ClientId: tournament.clientId,
            MinimumWage: tournament.minimumWage || 0,
            PerDiem: tournament.perDiem || 0,
            TravelPerDiem: tournament.travelPerDiem || 0,
            BillRate: tournament.billRate || null,
            ManagerBillRate: tournament.managerBillRate || null,
            PrintBillRate: tournament.printBillRate || null,
            OTByDay: tournament.otByDay || false
        };

        const response = await fetch(ProcessUrl('Tournament/Edit'), {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const result = await response.json();
        if (!result.success) {
            throw new Error(result.message || 'Failed to update tournament');
        }

        console.log('Tournament updated successfully');
        this.showUserFeedback('Tournament updated successfully!', 'success');

        // Reload tournaments from database to get properly formatted dates
        await this.loadTournamentsFromDatabase();

        // Regenerate schedule rows and refresh grid to reflect updated tournament
        this.generateScheduleRows();
        this.applyTournamentFilter();
        this.createGrid();
    }

    async createTournamentDirectly(tournament) {
        console.log('Creating tournament in backend...', tournament);

        // Helper function to format date without timezone conversion
        const formatDateOnly = (date) => {
            if (!date) return null;
            const d = date instanceof Date ? date : new Date(date);
            const year = d.getFullYear();
            const month = String(d.getMonth() + 1).padStart(2, '0');
            const day = String(d.getDate()).padStart(2, '0');
            return `${year}-${month}-${day}`;
        };

        const startDateFormatted = formatDateOnly(tournament.startDate);
        const endDateFormatted = formatDateOnly(tournament.endDate);

        const payload = {
            Name: tournament.name,
            Description: tournament.name + ' Tournament',
            Location: tournament.location,
            StartDate: startDateFormatted,
            EndDate: endDateFormatted,
            SetupStartDate: formatDateOnly(tournament.setupStart),
            SetupEndDate: formatDateOnly(tournament.setupEnd),
            AdvanceWeekStartDate: formatDateOnly(tournament.advancedStart),
            AdvanceWeekEndDate: formatDateOnly(tournament.advancedEnd),
            TeardownStartDate: formatDateOnly(tournament.teardownStart),
            TeardownEndDate: formatDateOnly(tournament.teardownEnd),
            Status: 1, // Planned
            ClientId: tournament.clientId,
            MinimumWage: tournament.minimumWage || 0,
            PerDiem: tournament.perDiem || 0,
            TravelPerDiem: tournament.travelPerDiem || 0,
            BillRate: tournament.billRate || null,
            ManagerBillRate: tournament.managerBillRate || null,
            PrintBillRate: tournament.printBillRate || null,
            OTByDay: tournament.otByDay || false
        };

        const response = await fetch(ProcessUrl('Tournament/Create'), {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const result = await response.json();
        if (!result.success) {
            throw new Error(result.message || 'Failed to create tournament');
        }

        console.log('Tournament created successfully');
        this.showUserFeedback('Tournament created successfully!', 'success');

        // Reload tournaments to get the new ID from database
        await this.loadTournamentsFromDatabase();

        // Regenerate schedule rows and refresh grid to include new tournament
        this.generateScheduleRows();
        this.applyTournamentFilter();
        this.createGrid();
    }

    goToToday() {
        const today = new Date();

        // Find the row that matches today's date
        const todayRowIndex = this.scheduleRows.findIndex((row) => {
            const rowDate = new Date(row.date);
            return rowDate.toDateString() === today.toDateString();
        });

        if (todayRowIndex >= 0) {
            const todayCell = $(`.cell[data-row="${todayRowIndex}"]`).first();

            if (todayCell.length) {
                this.selectCell(todayCell);

                // Scroll to today's row
                const container = $(".spreadsheet-container");
                const cellOffset = todayCell.offset().top - container.offset().top;
                container.scrollTop(container.scrollTop() + cellOffset - 100);
            }
        } else {
            alert("Today's date is not in the current tournament schedule.");
        }
    }

    saveToStorage() {
        const saveData = {
            data: this.data,
            columnNames: this.columnNames,
            tournaments: this.tournaments,
            nextTournamentId: this.nextTournamentId,
        };
        localStorage.setItem(
            `tournament-schedule-data-${this.year}`,
            JSON.stringify(saveData)
        );
    }

    loadFromStorage() {
        const saved = localStorage.getItem(`tournament-schedule-data-${this.year}`);
        if (saved) {
            try {
                const saveData = JSON.parse(saved);
                this.data = saveData.data || {};
                this.columnNames = saveData.columnNames || this.columnNames; // Keep server data if no saved columns
                this.tournaments = (saveData.tournaments || []).map((tournament) => ({
                    ...tournament,
                    startDate: new Date(tournament.startDate),
                    endDate: new Date(tournament.endDate),
                    // Convert phase dates to Date objects if they exist
                    setupStart: tournament.setupStart ? new Date(tournament.setupStart) : undefined,
                    setupEnd: tournament.setupEnd ? new Date(tournament.setupEnd) : undefined,
                    advancedStart: tournament.advancedStart ? new Date(tournament.advancedStart) : undefined,
                    advancedEnd: tournament.advancedEnd ? new Date(tournament.advancedEnd) : undefined,
                    teardownStart: tournament.teardownStart ? new Date(tournament.teardownStart) : undefined,
                    teardownEnd: tournament.teardownEnd ? new Date(tournament.teardownEnd) : undefined,
                }));
                this.nextTournamentId = saveData.nextTournamentId || this.nextTournamentId;
                console.log(`Successfully loaded local storage data for year ${this.year}`);
                return true; // Data was found and loaded
            } catch (error) {
                console.error("Error loading data:", error);
                return false; // Error loading data
            }
        }
        return false; // No data found
    }

    // Initialize from server-provided data if localStorage is empty
    initializeFromServerData() {
        // Load column names from EventStaffData if available
        if (window.EventStaffData && window.EventStaffData.length > 0) {
            this.columnNames = window.EventStaffData.map(staff => staff.FullName);
            console.log(`Initialized ${this.columnNames.length} staff members from server data`);
        }

        // Load tournaments from TournamentsData if available
        if (window.TournamentsData && window.TournamentsData.length > 0) {
            this.tournaments = window.TournamentsData.map(tournament => ({
                id: tournament.Id,
                name: tournament.Name,
                startDate: new Date(tournament.StartDate),
                endDate: new Date(tournament.EndDate),
                setupStart: tournament.SetupStart ? new Date(tournament.SetupStart) : undefined,
                setupEnd: tournament.SetupEnd ? new Date(tournament.SetupEnd) : undefined,
                advancedStart: tournament.AdvancedStart ? new Date(tournament.AdvancedStart) : undefined,
                advancedEnd: tournament.AdvancedEnd ? new Date(tournament.AdvancedEnd) : undefined,
                teardownStart: tournament.TeardownStart ? new Date(tournament.TeardownStart) : undefined,
                teardownEnd: tournament.TeardownEnd ? new Date(tournament.TeardownEnd) : undefined,
            }));

            // Set next tournament ID
            if (this.tournaments.length > 0) {
                const maxId = Math.max(...this.tournaments.map(t => t.id || 0));
                this.nextTournamentId = maxId + 1;
            }

            console.log(`Initialized ${this.tournaments.length} tournaments from server data`);
        }
    }

    exportCSV() {
        let csv = "Tournament,Phase,Date";

        // Headers
        for (let col = 0; col < this.columnNames.length; col++) {
            csv += "," + this.columnNames[col];
        }
        csv += "\n";

        // Data rows
        this.scheduleRows.forEach((rowData, rowIndex) => {
            const dateString = rowData.date.toLocaleDateString();
            const rowHeader = `"${rowData.tournament.name}","${rowData.phase}","${dateString}"`;
            const cellData = [];

            for (let col = 0; col < this.columnNames.length; col++) {
                const cellId = `${col}_${rowIndex}`;
                const value = this.data[cellId]
                    ? this.data[cellId].value || ""
                    : "";
                cellData.push(`"${value}"`);
            }

            csv += rowHeader + "," + cellData.join(",") + "\n";
        });

        // Download
        const blob = new Blob([csv], { type: "text/csv" });
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = `tournament-schedule-${this.year}.csv`;
        a.click();
        window.URL.revokeObjectURL(url);
    }

    // Database integration placeholder for tournaments
    async loadTournamentsFromDatabase() {
        try {
            // Example API call:
            // const response = await fetch('/api/tournaments');
            // const tournaments = await response.json();
            // this.tournaments = tournaments;

            console.log("Tournament database integration not yet implemented");
            return this.tournaments;
        } catch (error) {
            console.error("Error loading tournaments from database:", error);
            return this.tournaments;
        }
    }

    populateYearSelector() {
        const selector = $("#year-selector");
        selector.empty();

        const currentYear = new Date().getFullYear();
        const startYear = currentYear - 5;
        const endYear = currentYear + 5;

        for (let year = startYear; year <= endYear; year++) {
            const option = $(`<option value="${year}">${year}</option>`);
            if (year === this.year) {
                option.prop("selected", true);
            }
            selector.append(option);
        }
    }


    // Load columns from server-side data with termination date filtering
    async loadColumnsFromDatabase() {
        try {
            // Use server-side data if available
            if (window.EventStaffData && window.EventStaffData.length > 0) {
                // Filter staff based on termination date and current year
                // Show staff if: no termination date OR termination year >= selected year
                const currentYear = this.year || new Date().getFullYear();
                const filteredStaff = window.EventStaffData.filter(staff => {
                    if (!staff.TerminationDate) return true;
                    // Handle .NET JSON date format: /Date(1234567890000)/
                    let terminationDate;
                    if (typeof staff.TerminationDate === 'string' && staff.TerminationDate.startsWith('/Date(')) {
                        const timestamp = parseInt(staff.TerminationDate.replace(/\/Date\((\d+)\)\//, '$1'));
                        terminationDate = new Date(timestamp);
                    } else {
                        terminationDate = new Date(staff.TerminationDate);
                    }
                    const terminationYear = terminationDate.getFullYear();
                    return terminationYear >= currentYear;
                });

                // Extract staff names from filtered data
                const staffNames = filteredStaff.map(staff => staff.FullName);

                // Add template columns for schedule templates
                const templateColumns = ['Template 1', 'Template 2', 'Template 3', 'Template 4', 'Template 5', 'Template 6', 'Template 7', 'Template 8', 'Template 9', 'Template 10', 'Template 11', 'Template 12', 'Template 13', 'Template 14', 'Template 15', 'Template 16', 'Template 17', 'Template 18', 'Template 19', 'Template 20', 'Template 21', 'Template 22', 'Template 23', 'Template 24', 'Template 25', 'Template 26'];

                this.columnNames = [...staffNames, ...templateColumns];
                return this.columnNames;
            }

            return this.columnNames;
        } catch (error) {
            console.error("Error loading from database:", error);
            return this.columnNames;
        }
    }


    formatCellValue(value) {
        if (typeof value === "number") {
            return value.toLocaleString();
        }
        return value;
    }

    updateCellReference(reference) {
        $("#cell-ref").text(reference);
    }

    handleKeyNavigation(e) {
        if (!this.selectedCell) return;

        const currentRow = parseInt(this.selectedCell.data("row"));
        const currentCol = parseInt(this.selectedCell.data("col"));
        let newRow = currentRow,
            newCol = currentCol;

        switch (e.key) {
            case "ArrowUp":
                e.preventDefault();
                newRow = Math.max(0, currentRow - 1);
                break;
            case "ArrowDown":
                e.preventDefault();
                newRow = Math.min(this.scheduleRows.length - 1, currentRow + 1);
                break;
            case "ArrowLeft":
                e.preventDefault();
                newCol = Math.max(0, currentCol - 1);
                break;
            case "ArrowRight":
                e.preventDefault();
                newCol = Math.min(this.columnNames.length - 1, currentCol + 1);
                break;
            case "Enter":
                e.preventDefault();
                $("#formula-input").focus();
                break;
            case "Delete":
                e.preventDefault();
                this.clearCell();
                break;
        }

        if (newRow !== currentRow || newCol !== currentCol) {
            const newCell = $(`.cell[data-row="${newRow}"][data-col="${newCol}"]`);
            if (newCell.length) {
                this.selectCell(newCell);
            }
        }
    }


    copyCell() {
        const cellsToCopy = this.selectedCells.length > 0 ? this.selectedCells : (this.selectedCell ? [this.selectedCell] : []);

        if (cellsToCopy.length > 0) {
            this.clipboard = {
                cells: cellsToCopy.map(cell => {
                    const rowIndex = parseInt(cell.data("row"));
                    const colIndex = parseInt(cell.data("col"));
                    const cellId = `${colIndex}_${rowIndex}`;
                    const cellData = this.data[cellId];
                    return {
                        cellId: cell.data("cell"), // Keep for reference
                        row: rowIndex,
                        col: colIndex,
                        value: cellData?.originalValue || cellData?.value || "",
                        style: cellData?.style || {},
                        assigned: cellData?.assigned || false,
                        tournament: cellData?.tournament || null,
                        tournamentId: cellData?.tournamentId || null,
                        phase: cellData?.phase || null,
                        notWorking: cellData?.notWorking || false,
                        travel: cellData?.travel || false,
                        role: cellData?.role || null,
                        dailyRate2: cellData?.dailyRate2 || null,
                        clockInUTC: cellData?.clockInUTC || null,
                        clockOutUTC: cellData?.clockOutUTC || null
                    };
                })
            };
        }
    }

    pasteCell() {
        if (!this.selectedCell || !this.clipboard || !this.clipboard.cells) return;

        const startCell = this.selectedCell;
        const startRow = parseInt(startCell.data("row"));
        const startCol = parseInt(startCell.data("col"));

        // Check if we're pasting assigned cells
        const hasAssignedCells = this.clipboard.cells.some(cellData => cellData.assigned);

        if (hasAssignedCells) {
            // Validate target cells for tournament assignment - allow partial paste
            const targetCells = [];
            let skippedCells = 0;

            for (const copiedCellData of this.clipboard.cells) {
                const targetRow = startRow + (copiedCellData.row - this.clipboard.cells[0].row);
                const targetCol = startCol + (copiedCellData.col - this.clipboard.cells[0].col);

                // Check if target cell exists
                if (targetRow < 0 || targetRow >= this.scheduleRows.length || targetCol < 0 || targetCol >= this.columnNames.length) {
                    skippedCells++;
                    continue; // Skip cells that extend beyond schedule
                }

                const targetCell = $(`.cell[data-row="${targetRow}"][data-col="${targetCol}"]`);
                const rowData = this.scheduleRows[targetRow];

                // Check if target has tournament activity
                if (!rowData.tournament && (!rowData.allTournaments || rowData.allTournaments.length === 0)) {
                    skippedCells++;
                    continue; // Skip cells without tournament activity
                }

                targetCells.push({
                    cell: targetCell,
                    rowData: rowData,
                    targetRow: targetRow,
                    targetCol: targetCol,
                    copiedData: copiedCellData
                });
            }

            // If no valid target cells, show error
            if (targetCells.length === 0) {
                alert("Cannot paste: No valid tournament days found in target area.");
                return;
            }

            // If some cells were skipped, notify user
            if (skippedCells > 0) {
                const pastedCount = targetCells.length;
                const totalCount = this.clipboard.cells.length;
                this.showUserFeedback(`Partial paste completed: ${pastedCount} of ${totalCount} blocks pasted. ${skippedCells} blocks skipped (no tournament days or beyond schedule bounds).`, 'warning');
            }

            // Check for multiple tournaments in target area
            const allTargetTournaments = new Set();
            targetCells.forEach(({ rowData }) => {
                if (rowData.allTournaments) {
                    rowData.allTournaments.forEach(activity => {
                        allTargetTournaments.add(activity.tournament.id);
                    });
                }
            });

            if (allTargetTournaments.size > 1) {
                // Show tournament selection modal for pasting
                this.showTournamentSelectionForPaste(targetCells);
                return;
            } else {
                // Single tournament or consistent tournaments, proceed with paste
                this.performPaste(targetCells);
            }
        } else {
            // Pasting non-assigned cells (regular data), proceed normally
            this.performSimplePaste();
        }
    }

    performSimplePaste() {
        console.log('=== PERFORMING SIMPLE PASTE ===');
        const startCell = this.selectedCell;
        const startRow = parseInt(startCell.data("row"));
        const startCol = parseInt(startCell.data("col"));
        console.log('Simple paste starting at row:', startRow, 'col:', startCol);

        let pastedCount = 0;
        let skippedCount = 0;

        for (const copiedCellData of this.clipboard.cells) {
            const targetRow = startRow + (copiedCellData.row - this.clipboard.cells[0].row);
            const targetCol = startCol + (copiedCellData.col - this.clipboard.cells[0].col);

            // Check bounds
            if (targetRow < 0 || targetRow >= this.scheduleRows.length || targetCol < 0 || targetCol >= this.columnNames.length) {
                skippedCount++;
                continue;
            }

            const targetCell = $(`.cell[data-row="${targetRow}"][data-col="${targetCol}"]`);

            if (targetCell.length) {
                this.setCellValue(targetCell, copiedCellData.value);

                // Ensure data structure exists
                const cellId = `${targetCol}_${targetRow}`;
                if (!this.data[cellId]) {
                    this.data[cellId] = { value: "", style: {} };
                }

                this.data[cellId].style = { ...copiedCellData.style };

                // Track this specific change for incremental saving
                if (this.trackCellChange) {
                    this.trackCellChange(targetRow, targetCol, this.data[cellId]);
                }

                // Apply styles visually (font size only)

                pastedCount++;
            } else {
                skippedCount++;
            }
        }

        // Show feedback if partial paste occurred
        if (skippedCount > 0) {
            const totalCount = this.clipboard.cells.length;
            this.showUserFeedback(`Partial paste completed: ${pastedCount} of ${totalCount} blocks pasted. ${skippedCount} blocks skipped (beyond schedule bounds).`, 'warning');
        }

        console.log('Simple paste completed, calling saveToStorage with force immediate...');
        this.saveToStorage(true); // Force immediate save for debugging
        console.log('=== SIMPLE PASTE COMPLETE ===');
    }

    performPaste(targetCells, specificTournament = null) {
        console.log('=== PERFORMING PASTE ===');
        console.log('Target cells count:', targetCells.length);
        console.log('Specific tournament:', specificTournament);

        targetCells.forEach(({ cell, rowData, targetRow, targetCol, copiedData }) => {
            if (!copiedData.assigned) return; // Skip non-assigned cells

            let tournamentToAssign = null;
            let phaseToAssign = null;

            if (specificTournament) {
                // Use the specified tournament from selection modal
                tournamentToAssign = specificTournament;

                // Find the activity for this specific tournament on this date
                if (rowData.allTournaments) {
                    const activity = rowData.allTournaments.find(act => act.tournament.id === specificTournament.id);
                    phaseToAssign = activity ? activity.phase : null;
                }
            } else {
                // Use the primary tournament from row data
                tournamentToAssign = rowData.tournament;
                phaseToAssign = rowData.phase;
            }

            if (!tournamentToAssign || !phaseToAssign) return;

            const targetCellId = `${targetCol}_${targetRow}`;

            // Create assignment similar to performAssignment method
            const phaseDisplayNames = {
                setup: "Setup",
                advanced: "Advanced Week",
                tournament: "Event",
                teardown: "Takedown"
            };

            const phaseDisplay = phaseDisplayNames[phaseToAssign] || phaseToAssign;
            const roleDisplay = copiedData.role ? `<div class="role-badge">${this.getRoleAbbreviation(copiedData.role)}</div>` : '';

            const cellContent = `
        <div class="cell-content-wrapper">
          <div class="cell-main-content">
            <div class="tournament-info">${tournamentToAssign.name}</div>
            <div class="assignment-status">${phaseDisplay}</div>
          </div>
          ${roleDisplay}
        </div>
      `;

            // Update the cell appearance
            cell.removeClass("not-working travel").addClass("assigned");
            cell.find(".cell-display").html(cellContent);

            // Apply tournament colors
            const tournamentColorMap = this.getTournamentColorMap();
            const tournamentColor = tournamentColorMap.get(tournamentToAssign.id);

            if (tournamentColor) {
                const textColor = this.getTextColor(tournamentColor);
                const borderColor = tournamentColor.match(/#([0-9a-f]{6})/i)?.[1] || "667eea";
                const currentCssText = cell.attr("style") || "";
                const newStyles = `background: ${tournamentColor} !important; color: ${textColor} !important; border-color: #${borderColor} !important;`;
                cell.attr("style", currentCssText + " " + newStyles);
            }

            // Apply phase border color
            if (phaseToAssign) {
                const phaseBorderColors = {
                    setup: '#000000',
                    advanced: '#334155',
                    tournament: '#FFFFFF',
                    teardown: '#6B7280'
                };
                const phaseBorderColor = phaseBorderColors[phaseToAssign];
                if (phaseBorderColor) {
                    const existingStyle = cell.attr("style") || "";
                    cell.attr("style", existingStyle + ` border-left: 4px solid ${phaseBorderColor} !important;`);
                }
            }

            // Store the assignment in data structure using correct row/col format
            if (!this.data[targetRow]) {
                this.data[targetRow] = {};
            }

            const cellData = {
                value: `${tournamentToAssign.name} - ${phaseDisplay}`,
                tournament: tournamentToAssign.id,
                tournamentId: tournamentToAssign.id,
                phase: phaseToAssign,
                assigned: true,
                isWorking: true,
                notWorking: false,
                travel: false,
                role: copiedData.role,
                style: copiedData.style || {},
                dailyRate2: copiedData.dailyRate2 || null,
                clockInUTC: copiedData.clockInUTC || null,
                clockOutUTC: copiedData.clockOutUTC || null
            };

            const cellId = `${targetCol}_${targetRow}`;
            this.data[cellId] = cellData;

            // Track this specific change for incremental saving
            if (this.trackCellChange) {
                this.trackCellChange(targetRow, targetCol, cellData);
            }
        });

        console.log('Paste completed, calling saveToStorage with force immediate...');
        this.saveToStorage(true); // Force immediate save for debugging
        console.log('=== PASTE COMPLETE ===');
    }

    isInModalOrInput(target) {
        // Check if the target is within a modal, input field, textarea, or other interactive element
        const $target = $(target);

        // Check if target is an input, textarea, or contenteditable element
        if ($target.is('input, textarea, [contenteditable="true"]') ||
            $target.closest('input, textarea, [contenteditable="true"]').length > 0) {
            return true;
        }

        // Check if target is within a modal
        if ($target.closest('.modal').length > 0) {
            return true;
        }

        // Check if target is within certain interactive elements
        if ($target.closest('.dropdown-menu, .context-menu').length > 0) {
            return true;
        }

        return false;
    }

    showTournamentSelectionForPaste(targetCells) {
        // Get all unique tournaments from target cells
        const availableTournaments = new Map();

        targetCells.forEach(({ rowData }) => {
            if (rowData.allTournaments) {
                rowData.allTournaments.forEach(activity => {
                    const tournamentId = activity.tournament.id;
                    if (!availableTournaments.has(tournamentId)) {
                        availableTournaments.set(tournamentId, {
                            tournament: activity.tournament,
                            count: 1
                        });
                    } else {
                        availableTournaments.get(tournamentId).count++;
                    }
                });
            }
        });

        // Populate the selection modal
        const listContainer = $("#tournament-selection-list");
        listContainer.empty();

        availableTournaments.forEach((data, tournamentId) => {
            const button = $(`
        <button type="button" class="btn btn-outline-primary d-block w-100 mb-2 tournament-selection-btn"
                data-tournament-id="${data.tournament.id}">
            <strong>${data.tournament.name}</strong><br>
            <small>Paste to ${data.count} cells</small>
        </button>
      `);

            button.click(() => {
                this.performPaste(targetCells, data.tournament);
                const modal = bootstrap.Modal.getInstance("#tournamentSelectionModal");
                modal.hide();
            });

            listContainer.append(button);
        });

        // Update modal title
        $("#tournamentSelectionModalLabel").text("Select Tournament for Paste");

        // Show the modal
        const modal = new bootstrap.Modal("#tournamentSelectionModal");
        modal.show();
    }

    clearCell() {
        const cellsToProcess =
            this.selectedCells.length > 0
                ? this.selectedCells
                : this.selectedCell
                    ? [this.selectedCell]
                    : [];

        let clearedCount = 0;
        cellsToProcess.forEach((cell) => {
            const rowIndex = cell.data("row");
            const colIndex = cell.data("col");

            // Check if there's actually data to clear
            const cellId = `${colIndex}_${rowIndex}`;
            if (this.data[cellId]) {
                // Track this change as a deletion (empty cell data)
                if (this.trackCellChange) {
                    this.trackCellChange(rowIndex, colIndex, null); // null indicates deletion
                }

                // Clear data using correct structure
                delete this.data[cellId];
                clearedCount++;

                // Clear visual display
                cell.html('<span class="cell-display"></span>');

                // Remove all styling classes including assignment ones and role classes
                cell.removeClass("assigned not-working travel role-manager role-laborer role-printer role-unassigned");

                // Reset all CSS properties including custom tournament styling
                cell.css({
                    "font-size": "",
                    color: "",
                    "background-color": "",
                    background: "",
                    "border-color": "",
                });

                // Also remove the entire style attribute to clear any !important styles
                cell.removeAttr("style");
            }
        });

        if (cellsToProcess.length === 1) {
            $("#formula-input").val("");
        }

        if (clearedCount > 0) {
            this.showUserFeedback(`Cleared ${clearedCount} assignment(s)`, 'success');

            // Detect if we're clearing template cells or regular cells
            const isTemplate = cellsToProcess.length > 0 &&
                this.columnNames[parseInt(cellsToProcess[0].data("col"))]?.startsWith('Template');

            // Send deletion requests based on cell type
            if (isTemplate) {
                this.sendTemplateDeletionRequests(cellsToProcess);
            } else {
                this.sendDeletionRequests(cellsToProcess);
            }

            this.saveToStorage();
        } else if (cellsToProcess.length > 0) {
            this.showUserFeedback('No assignments to clear', 'info');
        }
    }

    async sendDeletionRequests(cellsToProcess) {
        try {
            const deletionRequests = [];

            cellsToProcess.forEach(cell => {
                const rowIndex = parseInt(cell.data("row"));
                const colIndex = parseInt(cell.data("col"));
                const staffName = this.columnNames[colIndex];
                const scheduleRow = this.scheduleRows[rowIndex];

                // Find staff member's user ID
                const staffMember = window.EventStaffData?.find(s => s.FullName === staffName);
                if (staffMember && scheduleRow && scheduleRow.date) {
                    // Extract date
                    let formattedDate;
                    if (scheduleRow.date instanceof Date) {
                        formattedDate = scheduleRow.date.toISOString().split('T')[0];
                    } else {
                        formattedDate = new Date(scheduleRow.date).toISOString().split('T')[0];
                    }

                    deletionRequests.push({
                        UserId: staffMember.UserId,
                        Date: formattedDate
                    });
                }
            });

            if (deletionRequests.length > 0) {
                console.log('Sending deletion requests:', deletionRequests);

                const response = await fetch(ProcessUrl('Tournament/DeleteScheduleAssignments'), {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify(deletionRequests)
                });

                if (response.ok) {
                    const result = await response.json();
                    console.log('Deletion response:', result);
                }
            }
        } catch (error) {
            console.error('Error sending deletion requests:', error);
        }
    }

    async sendTemplateDeletionRequests(cellsToProcess) {
        try {
            const deletionRequests = [];

            cellsToProcess.forEach(cell => {
                const rowIndex = parseInt(cell.data("row"));
                const colIndex = parseInt(cell.data("col"));
                const templateName = this.columnNames[colIndex];
                const scheduleRow = this.scheduleRows[rowIndex];

                // Only process if this is a template column
                if (templateName && templateName.startsWith('Template') && scheduleRow && scheduleRow.date) {
                    // Extract date
                    let formattedDate;
                    if (scheduleRow.date instanceof Date) {
                        formattedDate = scheduleRow.date.toISOString().split('T')[0];
                    } else {
                        formattedDate = new Date(scheduleRow.date).toISOString().split('T')[0];
                    }

                    deletionRequests.push({
                        TemplateName: templateName,
                        Date: formattedDate
                    });
                }
            });

            if (deletionRequests.length > 0) {
                console.log('Sending template deletion requests:', deletionRequests);

                const response = await fetch(ProcessUrl('Tournament/DeleteScheduleTemplateAssignments'), {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify(deletionRequests)
                });

                if (response.ok) {
                    const result = await response.json();
                    console.log('Template deletion response:', result);
                }
            }
        } catch (error) {
            console.error('Error sending template deletion requests:', error);
        }
    }


    showContextMenu(x, y) {
        const menu = $("#context-menu");

        // Update menu options based on selected cell state
        if (this.selectedCell) {
            this.updateContextMenuForSelectedCell();
        }

        // Show menu first to get its dimensions
        menu.css({ display: "block", left: "0px", top: "0px" });

        // Get menu dimensions
        const menuWidth = menu.outerWidth();
        const menuHeight = menu.outerHeight();

        // Get viewport dimensions
        const viewportWidth = $(window).width();
        const viewportHeight = $(window).height();
        const scrollTop = $(window).scrollTop();
        const scrollLeft = $(window).scrollLeft();

        // Calculate adjusted position to keep menu in viewport
        let adjustedX = x;
        let adjustedY = y;

        // Check right edge
        if (x + menuWidth > scrollLeft + viewportWidth) {
            adjustedX = Math.max(scrollLeft + 10, x - menuWidth);
        }

        // Check bottom edge
        if (y + menuHeight > scrollTop + viewportHeight) {
            adjustedY = Math.max(scrollTop + 10, y - menuHeight);
        }

        // Ensure menu doesn't go off left or top edges
        adjustedX = Math.max(scrollLeft + 10, adjustedX);
        adjustedY = Math.max(scrollTop + 10, adjustedY);

        // Apply final position
        menu.css({
            left: adjustedX + "px",
            top: adjustedY + "px",
        });
    }

    hideContextMenu() {
        $("#context-menu").hide();
        $("#column-context-menu").hide();
    }

    showColumnContextMenu(x, y) {
        // Create or show column context menu
        let menu = $("#column-context-menu");
        if (menu.length === 0) {
            menu = $(`
                <div class="context-menu" id="column-context-menu">
                    <div class="context-menu-item" data-action="copy-template-to-column">
                        <i class="bi bi-clipboard-plus"></i> Copy Template to This Column
                    </div>
                    <div class="context-menu-item" data-action="copy-column-as-template">
                        <i class="bi bi-clipboard-check"></i> Copy This Column as Template
                    </div>
                    <div class="context-menu-separator"></div>
                    <div class="context-menu-item" data-action="clear-entire-column">
                        <i class="bi bi-x-circle"></i> Clear Entire Column
                    </div>
                </div>
            `);
            $("body").append(menu);

            // Handle column context menu clicks
            menu.on("click", ".context-menu-item", (e) => {
                const action = $(e.currentTarget).data("action");
                this.handleColumnContextAction(action);
                this.hideContextMenu();
            });
        }

        // Update menu options based on selected column
        this.updateColumnContextMenu();

        // Position and show menu
        menu.css({ left: x + "px", top: y + "px" }).show();
    }

    updateColumnContextMenu() {
        if (!this.selectedColumn) return;

        const colIndex = parseInt(this.selectedColumn.data("col"));
        const staffName = this.columnNames[colIndex];
        const isTemplate = staffName && staffName.startsWith('Template');

        // Check if user column has assignments
        let hasAssignments = false;
        if (!isTemplate) {
            for (let rowIndex = 0; rowIndex < this.scheduleRows.length; rowIndex++) {
                const cellId = `${colIndex}_${rowIndex}`;
                if (this.data[cellId] && (this.data[cellId].tournamentId || this.data[cellId].travel || this.data[cellId].notWorking)) {
                    hasAssignments = true;
                    break;
                }
            }
        }

        // Show/hide options based on column type
        $("#column-context-menu .context-menu-item[data-action='copy-template-to-column']")
            .toggle(!isTemplate); // Only show for user columns

        $("#column-context-menu .context-menu-item[data-action='copy-column-as-template']")
            .toggle(!isTemplate && hasAssignments); // Only show for user columns with assignments

        // Clear entire column option is always available
        $("#column-context-menu .context-menu-item[data-action='clear-entire-column']")
            .show();
    }

    updateContextMenuForSelectedCell() {
        if (!this.selectedCell) return;

        // Check all selected cells to determine menu state
        let hasAnyAssignment = false;
        let hasAnyTournament = false;
        let allHaveAssignments = true;
        let allHaveTournaments = true;

        // Determine which data source to use (filteredData when filter is active, otherwise data)
        const dataSource = this.filteredData && this.filteredTournamentIds.size > 0 ? this.filteredData : this.data;

        // If we have multiple selected cells, check all of them
        const cellsToCheck = this.selectedCells && this.selectedCells.length > 1 ? this.selectedCells : [this.selectedCell];

        cellsToCheck.forEach($cell => {
            const rowIndex = $cell.data('row');
            const colIndex = $cell.data('col');
            const cellId = `${colIndex}_${rowIndex}`;
            const cellData = dataSource[cellId];
            const scheduleRow = this.scheduleRows[rowIndex];

            const hasAssignment = cellData && (cellData.tournamentId || cellData.tournament || cellData.assigned);
            const hasTournament = scheduleRow && (scheduleRow.tournament || (scheduleRow.allTournaments && scheduleRow.allTournaments.length > 0));

            if (hasAssignment) hasAnyAssignment = true;
            else allHaveAssignments = false;

            if (hasTournament) hasAnyTournament = true;
            else allHaveTournaments = false;
        });

        // Use primary selected cell for specific info
        const $cell = this.selectedCell;
        const rowIndex = $cell.data('row');
        const scheduleRow = this.scheduleRows[rowIndex];

        // Update "Assign to Tournament" option
        const assignOption = $('.context-menu-item[data-action="assign"]');
        if (hasAnyTournament) {
            assignOption.removeClass('disabled');
            if (scheduleRow && scheduleRow.tournament) {
                assignOption.attr('title', `Assign to ${scheduleRow.tournament.name}`);
            } else {
                assignOption.attr('title', 'Assign to tournament');
            }
        } else {
            assignOption.addClass('disabled').attr('title', 'No tournament scheduled for this date');
        }

        // Update "Clear Assignment" option  
        $('.context-menu-item[data-action="clear"]').toggleClass('disabled', !hasAnyAssignment);

        // Update role options - show if ANY selected cell has an assignment
        const roleMenuVisible = hasAnyAssignment;
        $('#dynamic-roles-container .context-menu-item').toggle(roleMenuVisible);
        $('.context-menu-item[data-action="role-unassigned"]').toggle(roleMenuVisible);
        $('#role-separator').toggle(roleMenuVisible);

        // Travel and not-working options are always available (no longer require tournament assignment)
        $('.context-menu-item[data-action="travel"]').removeClass('disabled');
        $('.context-menu-item[data-action="not-working"]').removeClass('disabled');

        // Show/hide clock times option - only show if there are tournament assignments
        $('.context-menu-item[data-action="set-clock-times"]').toggle(hasAnyAssignment);

        // Show/hide clock times option - only show if there are tournament assignments
        $('.context-menu-item[data-action="change-rate-type"]').toggle(hasAnyAssignment);

        // Update clock times menu text based on whether times already exist
        const hasAnyClockTimes = cellsToCheck.some($cell => {
            const rowIndex = $cell.data('row');
            const colIndex = $cell.data('col');
            const cellId = `${colIndex}_${rowIndex}`;
            const cellData = dataSource[cellId];
            return cellData && cellData.hasClockTimes;
        });

        const clockTimesText = hasAnyClockTimes ? 'Update Clock-In/Clock-Out Times' : 'Set Clock-In/Clock-Out Times';
        $('.context-menu-item[data-action="set-clock-times"]').html(`<i class="bi bi-clock"></i> ${clockTimesText}`);

        // Manage separators - hide separators that have no visible items around them
        this.manageSeparators();

    }

    manageSeparators() {
        const $contextMenu = $('#context-menu');
        const $items = $contextMenu.children();

        $items.each(function (index) {
            const $item = $(this);

            if ($item.hasClass('context-menu-separator')) {
                // Check if there are visible menu items before and after this separator
                let hasVisibleBefore = false;
                let hasVisibleAfter = false;

                // Check items before this separator
                for (let i = index - 1; i >= 0; i--) {
                    const $prevItem = $items.eq(i);
                    if ($prevItem.hasClass('context-menu-separator')) {
                        break; // Stop at the previous separator
                    }
                    if ($prevItem.is(':visible') && !$prevItem.hasClass('disabled')) {
                        hasVisibleBefore = true;
                        break;
                    }
                }

                // Check items after this separator
                for (let i = index + 1; i < $items.length; i++) {
                    const $nextItem = $items.eq(i);
                    if ($nextItem.hasClass('context-menu-separator')) {
                        break; // Stop at the next separator
                    }
                    if ($nextItem.is(':visible') && !$nextItem.hasClass('disabled')) {
                        hasVisibleAfter = true;
                        break;
                    }
                }

                // Show separator only if there are visible items both before and after
                $item.toggle(hasVisibleBefore && hasVisibleAfter);
            }
        });
    }

    showUserFeedback(message, type = 'info') {
        // Use the existing feedback system from the view if available
        if (window.showError && type === 'warning') {
            window.showError(message);
        } else if (window.showSuccess && type === 'success') {
            window.showSuccess(message);
        } else {
            // Fallback to console and browser alert
            console.log(`User feedback (${type}): ${message}`);

            // Create a temporary toast notification
            const toast = $(`
                <div class="user-feedback-toast ${type}" style="
                    position: fixed;
                    top: 20px;
                    right: 20px;
                    background: ${type === 'warning' ? '#fff3cd' : type === 'success' ? '#d1edff' : '#e2e3e5'};
                    color: ${type === 'warning' ? '#856404' : type === 'success' ? '#0c5460' : '#383d41'};
                    border: 1px solid ${type === 'warning' ? '#ffeaa7' : type === 'success' ? '#bee5eb' : '#d1d3d4'};
                    border-radius: 6px;
                    padding: 12px 16px;
                    box-shadow: 0 4px 12px rgba(0,0,0,0.15);
                    z-index: 9999;
                    max-width: 350px;
                    font-size: 14px;
                ">
                    <i class="bi bi-${type === 'warning' ? 'exclamation-triangle' : type === 'success' ? 'check-circle' : 'info-circle'}"></i>
                    ${message}
                </div>
            `);

            $('body').append(toast);

            // Fade out and remove after 3 seconds
            setTimeout(() => {
                toast.fadeOut(300, function() {
                    $(this).remove();
                });
            }, 3000);
        }
    }

    handleContextAction(action) {
        // Check if the clicked menu item is disabled
        const menuItem = $(`.context-menu-item[data-action="${action}"]`);
        if (menuItem.hasClass('disabled')) {
            const tooltip = menuItem.attr('title') || 'This action is not available';
            this.showUserFeedback(tooltip, 'warning');
            return;
        }

        switch (action) {
            case "copy":
                this.copyCell();
                break;
            case "paste":
                this.pasteCell();
                break;
            case "clear":
                const clearRowIndex = this.selectedCell.data('row');
                const clearColIndex = this.selectedCell.data('col');
                const clearCellId = `${clearColIndex}_${clearRowIndex}`;
                if (!this.selectedCell || !this.data[clearCellId]) {
                    this.showUserFeedback('No assignment to clear', 'warning');
                    return;
                }
                this.clearCell();
                break;
            case "travel":
                if (!this.selectedCell) {
                    this.showUserFeedback('Please select a cell first', 'warning');
                    return;
                }
                this.markTravel();
                break;
            case "assign":
                // Check if ANY of the selected cells have a tournament
                const cellsToCheck = this.selectedCells && this.selectedCells.length > 0 ? this.selectedCells : [this.selectedCell];
                const hasAnyTournament = cellsToCheck.some(cell => {
                    const rowIndex = cell.data('row');
                    const scheduleRow = this.scheduleRows[rowIndex];
                    return scheduleRow && (scheduleRow.tournament || (scheduleRow.allTournaments && scheduleRow.allTournaments.length > 0));
                });

                if (!hasAnyTournament) {
                    this.showUserFeedback('No tournament scheduled for the selected dates', 'warning');
                    return;
                }
                this.assignToTournament();
                break;
            case "not-working":
                if (!this.selectedCell) {
                    this.showUserFeedback('Please select a cell first', 'warning');
                    return;
                }
                this.markNotWorking();
                break;
            case "role-manager":
                this.setRole("Manager");
                break;
            case "role-laborer":
                this.setRole("General Laborer");
                break;
            case "role-printer":
                this.setRole("Printer");
                break;
            case "role-unassigned":
                this.setRole("Unassigned");
                break;
            case "change-rate-type":
                this.openChangeRateTypeModal();
                break;
            case "set-clock-times":
                this.openClockTimesModal();
                break;
        }
    }

    assignRoleToSelectedCell(roleId, roleName, roleRate) {
        console.log(`Assigning role: ${roleName} (ID: ${roleId}, Rate: ${roleRate}) to selected cells`);

        if (this.selectedCells.length === 0) {
            this.showUserFeedback('No cells selected', 'warning');
            return;
        }

        // Determine if filtering is active
        const isFiltering = this.filteredData && this.filteredTournamentIds.size > 0;
        const dataSource = isFiltering ? this.filteredData : this.data;

        // Apply role to all selected cells that have tournament assignments
        let assignedCount = 0;
        this.selectedCells.forEach($cell => {
            const rowIndex = $cell.data('row');
            const colIndex = $cell.data('col');
            const cellId = `${colIndex}_${rowIndex}`;
            const cellData = dataSource[cellId];

            if (cellData && cellData.tournamentId) {
                // Update the cell data with role information
                cellData.role = roleName;
                cellData.roleId = roleId;
                cellData.dailyRate = roleRate;

                // If filtering is active, also update the original data for persistence
                if (isFiltering) {
                    const originalRowIndex = this.getOriginalRowIndex(rowIndex);
                    if (originalRowIndex !== -1) {
                        const originalCellId = `${colIndex}_${originalRowIndex}`;
                        if (this.data[originalCellId]) {
                            this.data[originalCellId].role = roleName;
                            this.data[originalCellId].roleId = roleId;
                            this.data[originalCellId].dailyRate = roleRate;
                        }
                    }
                }

                // Track this change for incremental saving
                if (this.trackCellChange) {
                    const trackRowIndex = isFiltering ? this.getOriginalRowIndex(rowIndex) : rowIndex;
                    if (trackRowIndex !== -1) {
                        this.trackCellChange(trackRowIndex, colIndex, cellData);
                    }
                }

                // Update visual display
                this.updateCellDisplay($cell, cellData);
                assignedCount++;
            }
        });

        if (assignedCount > 0) {
            this.showUserFeedback(`Role "${roleName}" assigned to ${assignedCount} cell(s)`, 'success');
            this.saveToStorage();
        } else {
            this.showUserFeedback('No tournament assignments found to assign role to', 'warning');
        }
    }

    // Helper method to get original row index from filtered row index
    getOriginalRowIndex(filteredRowIndex) {
        if (!this.filteredTournamentIds || this.filteredTournamentIds.size === 0) {
            return filteredRowIndex; // No filtering active
        }

        const filteredRow = this.scheduleRows[filteredRowIndex];
        if (!filteredRow || !this.allScheduleRows) {
            return -1;
        }

        // Find the original row with the same date
        return this.allScheduleRows.findIndex(row =>
            row.date.getTime() === filteredRow.date.getTime()
        );
    }

    updateCellDisplay($cell, cellData) {
        if (!cellData) {
            $cell.html('').removeClass('assigned');
            return;
        }

        // Normalize to jQuery object no matter what
        if (!($cell instanceof jQuery)) {
            $cell = $($cell);
        }
        console.log($cell, $cell instanceof jQuery);


        // Build display content using the same format as paste function
        let displayContent = '';
        let cssClasses = 'assigned';

        // Prioritize notWorking and travel flags regardless of tournamentId
        if (cellData.notWorking) {
            // Determine absence type label based on EntryType
            let absenceLabel = 'Not Working';  // Default for backward compatibility
            switch(cellData.entryType) {
                case 3:
                    absenceLabel = 'Sick';
                    break;
                case 4:
                    absenceLabel = 'Time Off - Offsite';
                    break;
                case 5:
                    absenceLabel = 'Time Off - Onsite';
                    break;
            }

            // Not working day - show tournament name if available to indicate which tournament they're not working
            if (cellData.tournamentId) {
                const tournament = this.tournaments.find(t => t.id === cellData.tournamentId);
                const tournamentName = tournament ? tournament.name : 'Tournament';
                displayContent = `
                    <div class="cell-content-wrapper">
                        <div class="cell-main-content">
                            <div class="tournament-info">${tournamentName}</div>
                            <div class="assignment-status">${absenceLabel}</div>
                        </div>
                    </div>
                `;
            } else {
                // No tournament associated
                displayContent = `<div class="cell-content-wrapper"><div class="cell-main-content">${absenceLabel}</div></div>`;
            }
            cssClasses += ' not-working';
        } else if (cellData.travel || cellData.isTravel) {
            // Travel day - check after notWorking
            // Determine destination tournament: use travelTournamentId if set, otherwise tournamentId
            const destinationTournamentId = cellData.travelTournamentId || cellData.tournamentId;
            if (destinationTournamentId) {
                const tournament = this.tournaments.find(t => t.id === destinationTournamentId);
                const tournamentName = tournament ? tournament.name : 'Tournament';
                displayContent = `
                    <div class="cell-content-wrapper">
                        <div class="cell-main-content">
                            <div class="tournament-info">${tournamentName}</div>
                            <div class="assignment-status">Travel</div>
                        </div>
                    </div>
                `;
            } else {
                displayContent = '<div class="cell-content-wrapper"><div class="cell-main-content">Travel</div></div>';
            }
            cssClasses += ' travel';
        } else if (cellData.tournamentId) {
            // Find tournament info
            const tournament = this.tournaments.find(t => t.id === cellData.tournamentId);
            const tournamentName = tournament ? tournament.name : 'Tournament';

            // Determine phase display
            const phaseDisplayNames = {
                setup: "Setup",
                advanced: "Advanced Week",
                tournament: "Event",
                teardown: "Takedown"
            };
            const phaseDisplay = phaseDisplayNames[cellData.phase] || cellData.phase || "Working";

            // Create role badge (top-right corner)
            const roleDisplay = cellData.role ? `<div class="role-badge">${this.getRoleAbbreviation(cellData.role)}</div>` : '';

            // Build content using same structure as paste function
            displayContent = `
                <div class="cell-content-wrapper">
                    <div class="cell-main-content">
                        <div class="tournament-info">${tournamentName}</div>
                        <div class="assignment-status">${phaseDisplay}</div>
                    </div>
                    ${roleDisplay}
                </div>
            `;

            // Handle special states for tournament assignments
            if (cellData.isTravel) {
                cssClasses += ' travel';
            }

            if (cellData.isWorking === false || cellData.notWorking) {
                cssClasses += ' not-working';
            }

            // Apply phase class for border color
            if (cellData.phase) {
                cssClasses += ` phase-${cellData.phase}`;
            }
        }

        $cell.html(displayContent).attr('class', `cell ${cssClasses}`);

        // Clear any inline styles first (to allow CSS classes to take effect)
        $cell.removeAttr('style');

        // Apply tournament background colors like the paste function does
        // BUT skip if it's a "Not Working" or "Travel" day (they use their own CSS styling)
        if (cellData.tournamentId && !cellData.notWorking && !cellData.travel && !cellData.isTravel) {
            const tournamentColorMap = this.getTournamentColorMap();
            const tournamentColor = tournamentColorMap.get(cellData.tournamentId);

            if (tournamentColor) {
                // Get appropriate text color
                const textColor = this.getTextColor(tournamentColor);
                const borderColor = tournamentColor.match(/#([0-9a-f]{6})/i)?.[1] || "667eea";

                // Apply tournament styling
                const currentCssText = $cell.attr("style") || "";
                const newStyles = `background: ${tournamentColor} !important; color: ${textColor} !important; border-color: #${borderColor} !important;`;
                $cell.attr("style", currentCssText + " " + newStyles);
            }
        }

        // Apply tournament colors for travel cells based on destination tournament
        if (cellData.travel || cellData.isTravel) {
            const destinationTournamentId = cellData.travelTournamentId || cellData.tournamentId;
            if (destinationTournamentId) {
                const tournamentColorMap = this.getTournamentColorMap();
                const tournamentColor = tournamentColorMap.get(destinationTournamentId);

                if (tournamentColor) {
                    const textColor = this.getTextColor(tournamentColor);
                    const borderColor = tournamentColor.match(/#([0-9a-f]{6})/i)?.[1] || "667eea";

                    const currentCssText = $cell.attr("style") || "";
                    const newStyles = `background: ${tournamentColor} !important; color: ${textColor} !important; border-color: #${borderColor} !important;`;
                    $cell.attr("style", currentCssText + " " + newStyles);
                }
            }
        }

        // Apply phase border color (after tournament colors so it takes precedence)
        if (cellData.phase) {
            const phaseBorderColors = {
                setup: '#000000',
                advanced: '#334155',
                tournament: '#FFFFFF',
                teardown: '#6B7280'
            };
            const phaseBorderColor = phaseBorderColors[cellData.phase];
            if (phaseBorderColor) {
                const existingStyle = $cell.attr("style") || "";
                $cell.attr("style", existingStyle + ` border-left: 4px solid ${phaseBorderColor} !important;`);
            }
        }
    }

    abbreviateTournamentName(name) {
        // Create abbreviation from tournament name
        if (!name) return 'T';
        return name.split(' ').map(word => word.charAt(0).toUpperCase()).join('').substring(0, 3);
    }

    abbreviateRole(role) {
        // Create abbreviation from role name
        if (!role) return '?';

        const abbreviations = {
            'Manager': 'MGR',
            'General Laborer': 'LAB',
            'Printer': 'PRT',
            'Unassigned': 'UNA'
        };

        return abbreviations[role] || role.substring(0, 3).toUpperCase();
    }

    setRole(role) {
        if (this.selectedCells.length === 0) return;

        // Only allow role assignment to cells that are assigned to tournaments
        this.selectedCells.forEach((cell) => {
            const rowIndex = parseInt(cell.data("row"));
            const colIndex = parseInt(cell.data("col"));


            const cellId = `${colIndex}_${rowIndex}`;
            const cellData = this.data[cellId];

            // Only set role if cell is assigned to a tournament
            if (cellData && cellData.assigned && cellData.tournament) {
                // Update the role in data
                this.data[cellId].role = role;

                // Track this change for incremental saving
                if (this.trackCellChange) {
                    this.trackCellChange(rowIndex, colIndex, this.data[cellId]);
                }

                // Remove existing role classes
                cell.removeClass("role-manager role-laborer role-printer role-unassigned");

                // Add new role class
                const roleClass = `role-${role.toLowerCase().replace(/\s+/g, "-")}`;
                cell.addClass(roleClass);

                // Update cell content to include role
                const tournament = this.tournaments.find((t) => t.id == cellData.tournament);
                if (tournament) {
                    const phaseDisplayNames = {
                        setup: "Setup",
                        advanced: "Advanced Week",
                        tournament: "Event",
                        teardown: "Takedown",
                    };
                    const phaseDisplay = phaseDisplayNames[cellData.phase] || cellData.phase || "Working";

                    // Get role abbreviation
                    const roleAbbrev = this.getRoleAbbreviation(role);

                    const cellContent = `
            <div class="cell-content-wrapper">
              <div class="cell-main-content">
                <div class="tournament-info">${tournament.name}</div>
                <div class="assignment-status">${phaseDisplay}</div>
              </div>
              <div class="role-badge">${roleAbbrev}</div>
            </div>
          `;

                    cell.find(".cell-display").html(cellContent);
                }
            }
        });

        this.saveToStorage();
    }

    getRoleAbbreviation(role) {
        const abbreviations = {
            "Manager": "M",
            "General Laborer": "GL",
            "Printer": "P",
            "Unassigned": "U"
        };
        return abbreviations[role] || "?";
    }

    deselectCell() {
        this.clearAllSelections();
        // Remove any date cell highlights
        $('.row-header.date-highlighted').removeClass('date-highlighted');
        if (this.selectedCell) {
            this.finishCellEditing(this.selectedCell);
            this.selectedCell.removeClass("selected");
            this.selectedCell = null;
            $("#cell-ref").text("");
            $("#formula-input").val("");
        }
    }

    exportCSV() {
        let csv = "Date";

        // Headers
        for (let col = 0; col < this.columnNames.length; col++) {
            csv += "," + this.columnNames[col];
        }
        csv += "\n";

        // Data rows
        for (let rowIndex = 0; rowIndex < this.scheduleRows.length; rowIndex++) {
            const scheduleRow = this.scheduleRows[rowIndex];
            let dateString = "";

            // Extract date string from schedule row
            if (scheduleRow && scheduleRow.date instanceof Date) {
                dateString = scheduleRow.date.toISOString().split('T')[0];
            } else if (scheduleRow instanceof Date) {
                dateString = scheduleRow.toISOString().split('T')[0];
            } else if (typeof scheduleRow === 'string') {
                dateString = scheduleRow;
            }

            const rowData = [dateString];

            for (let colIndex = 0; colIndex < this.columnNames.length; colIndex++) {
                const cellId = `${colIndex}_${rowIndex}`;
                const value = this.data[cellId]
                    ? (this.data[cellId].value || "")
                    : "";
                rowData.push(`"${value}"`);
            }
            csv += rowData.join(",") + "\n";
        }

        // Download
        const blob = new Blob([csv], { type: "text/csv" });
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = `calendar-${this.year}.csv`;
        a.click();
        window.URL.revokeObjectURL(url);
    }

    getTournamentColorMap() {
        const tournamentColorMap = new Map();
        // 20 solid, distinct colors - arranged so adjacent colors are visually different
        const colors = [
            "#2563EB", // Blue
            "#DC2626", // Red
            "#059669", // Green
            "#7C3AED", // Purple
            "#D97706", // Orange
            "#0891B2", // Cyan
            "#BE185D", // Pink
            "#4338CA", // Indigo
            "#65A30D", // Lime
            "#9333EA", // Violet
            "#EA580C", // Dark Orange
            "#0D9488", // Teal
            "#E11D48", // Rose
            "#1D4ED8", // Royal Blue
            "#15803D", // Forest Green
            "#A21CAF", // Fuchsia
            "#CA8A04", // Yellow
            "#6366F1", // Slate Blue
            "#16A34A", // Emerald
            "#DB2777", // Magenta
        ];

        // Get all unique tournaments and assign colors
        const uniqueTournaments = new Set();
        this.tournaments.forEach((tournament) => {
            uniqueTournaments.add(tournament.id);
        });

        // Assign colors to tournaments (use numeric sort to match createGrid)
        // Colors are pre-arranged so that adjacent indices have visually distinct colors
        const sortedIds = Array.from(uniqueTournaments).sort((a, b) => a - b);
        sortedIds.forEach((tournamentId, index) => {
            const color = colors[index % colors.length];
            tournamentColorMap.set(tournamentId, color);
        });

        return tournamentColorMap;
    }

    getTextColor(colorString) {
        // Extract the hex color (works for both solid colors and gradients)
        const colorMatch = colorString.match(/#([0-9a-f]{6})/i);
        if (!colorMatch) return "white"; // fallback to white

        const hex = colorMatch[1];
        const r = parseInt(hex.substr(0, 2), 16);
        const g = parseInt(hex.substr(2, 2), 16);
        const b = parseInt(hex.substr(4, 2), 16);

        // Calculate relative luminance
        const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;

        // Use black text if background is light (luminance > 0.6)
        return luminance > 0.6 ? "black" : "white";
    }

    assignToTournament() {
        if (this.selectedCells.length === 0) return;

        // Check if any selected cells have multiple tournaments
        const hasOverlappingTournaments = this.selectedCells.some((cell) => {
            const rowIndex = cell.data("row");
            return (
                this.scheduleRows[rowIndex] &&
                this.scheduleRows[rowIndex].allTournaments &&
                this.scheduleRows[rowIndex].allTournaments.length > 1
            );
        });

        if (hasOverlappingTournaments) {
            // Show tournament selection modal
            this.showTournamentSelection();
            return;
        }

        // No overlaps, proceed with normal assignment
        this.performAssignment();
    }

    showTournamentSelection() {
        // Get all unique tournaments from selected cells (group by tournament only)
        const availableTournaments = new Map();

        this.selectedCells.forEach((cell) => {
            const rowIndex = cell.data("row");
            const rowData = this.scheduleRows[rowIndex];

            if (rowData && rowData.allTournaments) {
                rowData.allTournaments.forEach((activity) => {
                    const tournamentId = activity.tournament.id;
                    if (!availableTournaments.has(tournamentId)) {
                        availableTournaments.set(tournamentId, {
                            tournament: activity.tournament,
                            count: 1,
                        });
                    } else {
                        availableTournaments.get(tournamentId).count++;
                    }
                });
            }
        });

        // Populate the selection modal
        const listContainer = $("#tournament-selection-list");
        listContainer.empty();

        availableTournaments.forEach((data, tournamentId) => {
            const button = $(`
                        <button type="button" class="btn btn-outline-primary d-block w-100 mb-2 tournament-selection-btn"
                                data-tournament-id="${data.tournament.id}">
                            <strong>${data.tournament.name}</strong><br>
                            <small>(${data.count} cells)</small>
                        </button>
                    `);

            button.click(() => {
                this.performAssignment(data.tournament, null); // Pass null for phase to auto-determine
                const modal = bootstrap.Modal.getInstance("#tournamentSelectionModal");
                modal.hide();
            });

            listContainer.append(button);
        });

        // Update modal title for assignment context
        $("#tournamentSelectionModalLabel").text("Select Tournament for Assignment");

        // Show the modal
        const modal = new bootstrap.Modal("#tournamentSelectionModal");
        modal.show();
    }

    performAssignment(specificTournament = null, specificPhase = null) {
        if (this.selectedCells.length === 0) return;

        // If tournament is not specified, get it from the selected cell
        if (!specificTournament && this.selectedCells.length > 0) {
            const firstCell = this.selectedCells[0];
            const rowIndex = firstCell.data("row");
            const rowData = this.scheduleRows[rowIndex];

            if (rowData && rowData.tournament) {
                // Single tournament for this date
                specificTournament = rowData.tournament;
                specificPhase = rowData.phase;
            } else if (rowData && rowData.allTournaments &&
                rowData.allTournaments.length > 0) {
                // Multiple tournaments - use the first one (this should be handled by tournament selection)
                const firstActivity = rowData.allTournaments[0];
                specificTournament = firstActivity.tournament;
                specificPhase = firstActivity.phase;
            }
        }

        // Show the rate type modal before proceeding with assignment
        this.showRateTypeModal(specificTournament, specificPhase);
    }

    showRateTypeModal(specificTournament, specificPhase) {

        const modal = $('#rate-type-modal');

        // Update modal title for new assignment
        $('.modal-title', modal).text('Assignment Rate Type');

        // Display tournament information if available
        if (specificTournament) {
            this.displayTournamentInfo(modal, specificTournament);
        }

        // Reset form
        $('#salary-rate').prop('checked', true);
        $('#daily-rate-amount').val('');
        $('#daily-rate-input').hide();

        // Populate role dropdown (default to Unassigned for new assignments)
        this.populateRoleDropdown('Unassigned');

        // Handle radio button changes
        $('input[name="rateType"]').off('change').on('change', function() {
            if ($(this).val() === 'daily') {
                $('#daily-rate-input').show();
                $('#daily-rate-amount').focus();
            } else {
                $('#daily-rate-input').hide();
            }
        });

        // Handle confirm button
        $('#confirm-rate-type').off('click').on('click', () => {
            const rateType = $('input[name="rateType"]:checked').val();
            const dailyRate2 = rateType === 'daily' ? parseFloat($('#daily-rate-amount').val()) || 0 : null;
            // const billRate = rateType === 'daily' ? parseFloat($('#bill-rate-amount').val()) || null : null;
            const selectedRole = $('#assignment-role').val();

            // Validate daily rate if daily type is selected
            if (rateType === 'daily' && (!dailyRate2 || dailyRate2 <= 0)) {
                alert('Please enter a valid daily rate amount.');
                return;
            }

            modal.modal('hide');
            this.proceedWithAssignment(specificTournament, specificPhase, rateType, dailyRate2, null, selectedRole);
        });

        modal.modal('show');
    }

    populateRoleDropdown(selectedRole = 'Unassigned') {
        const roleSelect = $('#assignment-role');
        roleSelect.empty();

        // Add default option (no selected attribute)
        roleSelect.append(`<option value="Unassigned">Unassigned</option>`);

        // Add roles from window.TournamentRoles
        if (window.TournamentRoles && window.TournamentRoles.length > 0) {
            window.TournamentRoles.forEach(role => {
                if (role.Title && role.Title !== 'Unassigned') {
                    roleSelect.append(`<option value="${role.Title}">${role.Title}</option>`);
                }
            });
        }

        // Set the selected value AFTER all options are added
        roleSelect.val(selectedRole);

        // Try alternative method if first didn't work
        if (roleSelect.val() !== selectedRole) {
            roleSelect.find('option').prop('selected', false);
            roleSelect.find(`option[value="${selectedRole}"]`).prop('selected', true);
        }
    }

    openChangeRateTypeModal() {
        if (!this.selectedCells || this.selectedCells.length === 0) {
            alert('Please select assigned cells first.');
            return;
        }

        // Get affected assignments and tournaments
        const affectedAssignments = this.getAffectedAssignments('change-rate-type');
        const tournaments = [...new Set(affectedAssignments.map(a =>
            a.tournamentId))];

        this.selectedCells.forEach(($cell, index) => {
            const rowIndex = $cell.data('row');
            const colIndex = $cell.data('col');
            const cellId = `${colIndex}_${rowIndex}`;
            const cellData = this.data[cellId];
        });

        if (affectedAssignments.length === 0) {
            alert('No tournament assignments found in selected cells. Please select cells that have tournament assignments.');
            return;
        }

        //const tournaments = [...new Set(affectedAssignments.map(a => a.tournamentId))];

        if (tournaments.length > 1) {
            // Multiple tournaments - show selection modal first
            this.showTournamentSelectionForRateChange(tournaments, affectedAssignments);
        } else if (tournaments.length === 1) {
            // Single tournament - go directly to rate type modal
            this.showRateTypeModalForTournament(tournaments[0], affectedAssignments);
        } else {
            alert('No tournament assignments found in selected cells.');
        }
    }

    showTournamentSelectionForRateChange(tournamentIds, affectedAssignments) {
        // Create tournament selection modal content for rate changes
        const tournaments = tournamentIds.map(id => this.tournaments.find(t => t.id ===
            id)).filter(t => t);

        let selectionHtml = '';
        tournaments.forEach(tournament => {
            const assignmentCount = affectedAssignments.filter(a => a.tournamentId ===
                tournament.id).length;
            selectionHtml += `
              <div class="tournament-option p-3 mb-2 border rounded" style="cursor: pointer;"
   data-tournament-id="${tournament.id}">
                  <div class="d-flex justify-content-between align-items-center">
                      <div>
                          <strong>${tournament.name}</strong>
                          <br><small class="text-muted">${tournament.location || ''}</small>
                      </div>
                      <span class="badge bg-primary">${assignmentCount} assignment(s)</span>
                  </div>
              </div>
          `;
        });

        $('#tournament-selection-list').html(selectionHtml);

        // Handle tournament selection
        $('.tournament-option').off('click').on('click', (e) => {
            const tournamentId = parseInt($(e.currentTarget).data('tournament-id'));
            const filteredAssignments = affectedAssignments.filter(a => a.tournamentId ===
                tournamentId);
            $('#tournamentSelectionModal').modal('hide');
            this.showRateTypeModalForTournament(tournamentId, filteredAssignments);
        });

        // Show the modal
        $('#tournamentSelectionModal').modal('show');
    }

    showRateTypeModalForTournament(tournamentId, affectedAssignments) {
        const tournament = this.tournaments.find(t => t.id === tournamentId);
        const selectedCount = this.selectedCells.length;
        const affectedAssignmentsCount = affectedAssignments.length;
        const modal = $('#rate-type-modal');

        // Update modal title
        const modalTitle = selectedCount === 1 ? 'Change Rate Type' : `Change Rate Type (${affectedAssignmentsCount} assignments)`;
        $('.modal-title', modal).text(modalTitle);

        // Display tournament information
        this.displayTournamentInfo(modal, tournament);

        if (selectedCount === 1) {
            // Single selection - populate with current values
            const firstAssignment = affectedAssignments[0];
            const currentRateType = firstAssignment.rateType || (firstAssignment.dailyRate2 ?
                'daily' : 'salary');
            const currentDailyRate = firstAssignment.dailyRate2 || '';
            // const currentBillRate = firstAssignment.billRate || '';

            $('#salary-rate').prop('checked', currentRateType === 'salary');
            $('#daily-rate').prop('checked', currentRateType === 'daily');
            $('#daily-rate-amount').val(currentDailyRate);
            // $('#bill-rate-amount').val(currentBillRate);
            $('#daily-rate-input').toggle(currentRateType === 'daily');
            // $('#bill-rate-input').toggle(currentRateType === 'daily');

            // Populate role dropdown with current role pre-selected
            const currentRole = firstAssignment.role || 'Unassigned';
            this.populateRoleDropdown(currentRole);

        } else {
            // Multiple selection - reset to defaults (empty input)
            $('#salary-rate').prop('checked', true);
            $('#daily-rate').prop('checked', false);
            $('#daily-rate-amount').val('');
            $('#bill-rate-amount').val('');
            $('#daily-rate-input').hide();
            $('#bill-rate-input').hide();

            // Populate role dropdown (default to Unassigned for multiple selections)
            this.populateRoleDropdown('Unassigned');
        }

        // Handle rate type change - clear ALL change handlers like clock times modal does
        $('input[name="rateType"]').off('change').on('change', function () {
            if ($(this).val() === 'daily') {
                $('#daily-rate-input').show();
                $('#bill-rate-input').show();
                $('#daily-rate-amount').focus();
            } else {
                $('#daily-rate-input').hide();
                $('#bill-rate-input').hide();
            }
        });

        // Clear ALL existing event handlers to avoid conflicts
        $('#confirm-rate-type').off('click');

        // Handle confirmation
        $('#confirm-rate-type').on('click', () => {
            const newRateType = $('input[name="rateType"]:checked').val();
            const newDailyRate = newRateType === 'daily' ?
                parseFloat($('#daily-rate-amount').val()) || 0 : null;
            // const newBillRate = newRateType === 'daily' ?
            //     parseFloat($('#bill-rate-amount').val()) || null : null;
            const newRole = $('#assignment-role').val();

            // Validate daily rate if daily type is selected
            if (newRateType === 'daily' && (!newDailyRate || newDailyRate <= 0)) {
                alert('Please enter a valid daily rate amount.');
                return;
            }

            // Update only the affected assignments for the selected tournament
            this.updateRateTypeForSelectedCells(newRateType, newDailyRate, null, affectedAssignments, newRole);
            modal.modal('hide');
        });

        modal.modal('show');
    }

    displayTournamentInfo(modal, tournament) {
        // Find or create tournament info container
        let $infoContainer = modal.find('.tournament-info-display');
        if ($infoContainer.length === 0) {
            $infoContainer = $('<div class="tournament-info-display mb-3"></div>');
            modal.find('.modal-body').prepend($infoContainer);
        }

        // Hide if no tournament
        if (!tournament) {
            $infoContainer.hide();
            return;
        }

        let infoHtml = `
          <div class="p-3 bg-light rounded">
              <h6 class="mb-2"><i class="bi bi-trophy"></i>${tournament.name}</h6>
              <div class="row">
      `;

        // Add minimum wage if available
        if (tournament.minimumWage) {
            infoHtml += `
              <div class="col-sm-4">
                  <small class="text-muted">Minimum Wage:</small><br>
                  <strong>$${parseFloat(tournament.minimumWage).toFixed(2)}</strong>
              </div>
          `;
        }

        // Add per diem if available
        if (tournament.perDiem) {
            infoHtml += `
              <div class="col-sm-4">
                  <small class="text-muted">Per Diem:</small><br>
                  <strong>$${parseFloat(tournament.perDiem).toFixed(2)}</strong>
              </div>
          `;
        }

        if (tournament.travelPerDiem) {
            infoHtml += `
                  <div class="col-sm-4">
                      <small class="text-muted">Travel Per Diem:</small><br>
                      <strong>$${parseFloat(tournament.travelPerDiem).toFixed(2)}</strong>
                  </div>
              `;
        }

        infoHtml += `
              </div>
          `;

        // Add bill rates section
        const hasBillRates = tournament.billRate || tournament.managerBillRate || tournament.printBillRate || tournament.travelPerDiem;
        if (hasBillRates) {
            infoHtml += `
              <div class="row mt-2">
          `;

            // Add general bill rate
            if (tournament.billRate) {
                infoHtml += `
                  <div class="col-sm-4">
                      <small class="text-muted">Bill Rate:</small><br>
                      <strong>$${parseFloat(tournament.billRate).toFixed(2)}</strong>
                  </div>
              `;
            }

            // Add manager bill rate
            if (tournament.managerBillRate) {
                infoHtml += `
                  <div class="col-sm-4">
                      <small class="text-muted">Manager Bill Rate:</small><br>
                      <strong>$${parseFloat(tournament.managerBillRate).toFixed(2)}</strong>
                  </div>
              `;
            }

            // Add print bill rate
            if (tournament.printBillRate) {
                infoHtml += `
                  <div class="col-sm-4">
                      <small class="text-muted">Print Bill Rate:</small><br>
                      <strong>$${parseFloat(tournament.printBillRate).toFixed(2)}</strong>
                  </div>
              `;
            }

            // Add travel per diem

            infoHtml += `
              </div>
          `;
        }

        // Add OT calculation if available
        if (tournament.otByDay !== undefined) {
            const otText = tournament.otByDay === true ? 'Daily' : 'Weekly';
            infoHtml += `
              <div class="col-sm-4">
                  <small class="text-muted">OT Calculation:</small><br>
                  <strong>${otText}</strong>
              </div>
          `;
        }

        infoHtml += `
          </div>
      `;

        $infoContainer.html(infoHtml).show();
    }

    updateRateTypeForSelectedCells(rateType, dailyRate2, billRate, affectedAssignments, newRole = null) {
        let updatedCount = 0;
        // Work directly with the pre-filtered affected assignments
        affectedAssignments.forEach(assignment => {
            const cellId = assignment.cellId;
            const cellData = this.data[cellId];

            if (cellData) {
                // Update the cell data with new rate information
                cellData.rateType = rateType;

                // Set dailyRate2 to null if rate type is not daily
                if (rateType === 'daily') {
                    cellData.dailyRate2 = dailyRate2;
                    // cellData.billRate = billRate;
                } else {
                    cellData.dailyRate2 = null;
                    // cellData.billRate = null;
                }

                // Update role if provided
                if (newRole !== null) {
                    cellData.role = newRole;
                }

                // Extract row and column from cellId for tracking
                const [colIndex, rowIndex] = cellId.split('_').map(Number);

                // Track this change for incremental saving
                if (this.trackCellChange) {
                    this.trackCellChange(rowIndex, colIndex, cellData);
                }

                // Update cell display to reflect role changes
                const $cell = $(`.cell[data-row="${rowIndex}"][data-col="${colIndex}"]`);
                if ($cell.length > 0) {
                    this.updateCellDisplay($cell, cellData);
                }

                updatedCount++;
            } else {
                console.log(`Warning: Cell data not found for ${cellId}`);
            }
        });

        if (updatedCount > 0) {
            const rateText = rateType === 'daily' ? `Daily rate ($${dailyRate2})` :
                'Salary';
            this.showUserFeedback(`Rate type updated to "${rateText}" for ${updatedCount} assignment(s)`, 'success');
            this.saveToStorage(true);
        } else {
            this.showUserFeedback('No tournament assignments found to update',
                'warning');
        }
    }

    proceedWithAssignment(specificTournament = null, specificPhase = null, rateType = 'salary', dailyRate2 = null, billRate = null, selectedRole = 'Unassigned') {
        if (this.selectedCells.length === 0) return;

        // Map phase names to display names
        const phaseDisplayNames = {
            setup: "Setup",
            advanced: "Advanced Week",
            tournament: "Event",
            teardown: "Takedown",
        };

        // Process each selected cell that has a tournament
        this.selectedCells.forEach((cell) => {
            let tournamentToAssign, phaseToAssign;
            const rowIndex = cell.data("row");
            const rowData = this.scheduleRows[rowIndex];

            // Early validation: Check if this row has any tournament scheduled
            const hasTournamentOnDate = rowData && (rowData.tournament || (rowData.allTournaments && rowData.allTournaments.length > 0));
            if (!hasTournamentOnDate) {
                // Skip cells that don't have any tournament scheduled on this date
                return;
            }

            if (specificTournament && specificPhase) {
                // Use specified tournament and phase
                tournamentToAssign = specificTournament;
                phaseToAssign = specificPhase;

                // Verify this specific tournament is actually available on this date
                if (rowData.allTournaments) {
                    const activity = rowData.allTournaments.find(
                        (act) => act.tournament.id === specificTournament.id
                    );
                    if (!activity) {
                        // Tournament not available on this date, skip this cell
                        return;
                    }
                }
            } else if (specificTournament && !specificPhase) {
                // Auto-determine phase based on date and specified tournament
                tournamentToAssign = specificTournament;

                // Find the activity for this specific tournament on this date
                if (rowData && rowData.allTournaments) {
                    const activity = rowData.allTournaments.find(
                        (act) => act.tournament.id === specificTournament.id
                    );
                    phaseToAssign = activity ? activity.phase : null;
                }

                if (!phaseToAssign) return; // Skip if we can't determine phase
            } else {
                // Use cell's default tournament
                const tournamentId = cell.data("tournament");
                const phase = cell.data("phase");

                if (!tournamentId) return;

                tournamentToAssign = this.tournaments.find((t) => t.id == tournamentId);
                phaseToAssign = phase;
            }

            if (!tournamentToAssign) return;

            const phaseDisplay = phaseDisplayNames[phaseToAssign] || phaseToAssign;

            // Create the cell content (preserve existing role if any, otherwise use selected role)
            const cellRowIndex = parseInt(cell.data("row"));
            const cellColIndex = parseInt(cell.data("col"));
            const existingCellId = `${cellColIndex}_${cellRowIndex}`;
            const existingRole = this.data[existingCellId]
                ? this.data[existingCellId].role
                : selectedRole; // Use selected role for new assignments
            const roleDisplay = existingRole ? `<div class="role-badge">${this.getRoleAbbreviation(existingRole)}</div>` : '';

            const cellContent = `
        <div class="cell-content-wrapper">
          <div class="cell-main-content">
            <div class="tournament-info">${tournamentToAssign.name}</div>
            <div class="assignment-status">${phaseDisplay}</div>
          </div>
          ${roleDisplay}
        </div>
      `;

            // Get tournament color
            const tournamentColorMap = this.getTournamentColorMap();
            const tournamentColor = tournamentColorMap.get(tournamentToAssign.id);

            // Update the cell
            cell.removeClass("not-working travel").addClass("assigned");
            cell.find(".cell-display").html(cellContent);

            // Apply tournament background color to cell and update border
            if (tournamentColor) {
                // Get appropriate text color
                const textColor = this.getTextColor(tournamentColor);

                // Update border color to match tournament
                const borderColor =
                    tournamentColor.match(/#([0-9a-f]{6})/i)?.[1] || "667eea";

                // Use cssText to apply styles with !important
                const currentCssText = cell.attr("style") || "";
                const newStyles = `background: ${tournamentColor} !important; color: ${textColor} !important; border-color: #${borderColor} !important;`;
                cell.attr("style", currentCssText + " " + newStyles);
            }

            // Apply phase border color
            if (phaseToAssign) {
                const phaseBorderColors = {
                    setup: '#000000',
                    advanced: '#334155',
                    tournament: '#FFFFFF',
                    teardown: '#6B7280'
                };
                const phaseBorderColor = phaseBorderColors[phaseToAssign];
                if (phaseBorderColor) {
                    const existingStyle = cell.attr("style") || "";
                    cell.attr("style", existingStyle + ` border-left: 4px solid ${phaseBorderColor} !important;`);
                }
            }

            // Store the assignment in the data structure using row/col indices
            //const rowIndex = cell.data('row');
            const colIndex = cell.data('col');

            const cellId = `${colIndex}_${rowIndex}`;
            this.data[cellId] = {
                value: `${tournamentToAssign.name} - ${phaseDisplay}`,
                tournamentId: tournamentToAssign.id, // Use tournamentId to match save API
                tournament: tournamentToAssign.id,
                phase: phaseToAssign,
                assigned: true,
                isWorking: true, // Use isWorking to match save API
                notWorking: false,
                travel: false,
                isTravel: false, // Use isTravel to match save API
                role: existingRole, // Preserve existing role or use selected role
                rateType: rateType, // Store rate type (salary/daily)
                dailyRate2: dailyRate2, // Store DailyRate2 value
                // billRate: billRate, // Store BillRate value
                style: this.data[rowIndex]?.[colIndex]?.style || {},
            };

            // Track the change for database save
            this.trackCellChange(rowIndex, colIndex, this.data[cellId]);
        });

        this.saveToStorage();
    }

    markNotWorking() {
        if (this.selectedCells.length === 0) return;

        // Open the absence type modal (following the same pattern as openClockTimesModal)
        this.openAbsenceTypeModal();
    }

    openAbsenceTypeModal() {
        if (!this.selectedCells || this.selectedCells.length === 0) {
            alert('Please select cells first.');
            return;
        }

        // Gather all unique tournaments across all selected cells
        const tournamentMap = new Map(); // id -> {id, name}

        this.selectedCells.forEach((cell) => {
            const rowIndex = parseInt(cell.data("row"));
            const rowData = this.scheduleRows[rowIndex];

            if (rowData && rowData.allTournaments && rowData.allTournaments.length > 0) {
                rowData.allTournaments.forEach(t => {
                    if (!tournamentMap.has(t.tournament.id)) {
                        tournamentMap.set(t.tournament.id, {
                            id: t.tournament.id,
                            name: t.tournament.name
                        });
                    }
                });
            }
        });

        // Convert tournament map to array
        const availableTournaments = Array.from(tournamentMap.values());

        // Populate tournament dropdown
        const tournamentSelect = $('#absence-tournament');
        tournamentSelect.empty();

        if (availableTournaments.length > 1) {
            // Multiple tournaments - show dropdown
            tournamentSelect.append('<option value="">Select Tournament</option>');
            availableTournaments.forEach(t => {
                tournamentSelect.append(`<option value="${t.id}">${t.name}</option>`);
            });
            $('#absence-tournament-selection').show();
        } else if (availableTournaments.length === 1) {
            // Single tournament - auto-select
            tournamentSelect.append(`<option value="${availableTournaments[0].id}" selected>${availableTournaments[0].name}</option>`);
            $('#absence-tournament-selection').show();
        } else {
            // No tournaments - hide dropdown
            $('#absence-tournament-selection').hide();
        }

        // Reset to Sick (EntryType=3) as default
        $('#absence-sick').prop('checked', true);

        // Setup confirm button handler for THIS modal instance
        const self = this;
        $('#confirm-absence-type').off('click').on('click', function() {
            self.handleAbsenceTypeConfirm();
        });

        // Show the modal
        $('#absence-type-modal').modal('show');
    }

    handleAbsenceTypeConfirm() {
        const selectedEntryType = parseInt($('input[name="absenceType"]:checked').val());
        const selectedTournamentId = $('#absence-tournament').val();

        if (!selectedEntryType) {
            alert('Please select an absence type');
            return;
        }

        // Get tournament info
        let tournamentId = selectedTournamentId ? parseInt(selectedTournamentId) : null;

        let processedCount = 0;
        let skippedCount = 0;

        // Process each selected cell
        this.selectedCells.forEach($cell => {
            const rowIndex = parseInt($cell.data('row'));
            const colIndex = parseInt($cell.data('col'));
            const cellId = `${colIndex}_${rowIndex}`;
            const rowData = this.scheduleRows[rowIndex];

            // Check if selected tournament is available on this date
            let canApply = false;
            if (tournamentId) {
                // Tournament was selected - check if it exists on this date
                if (rowData && rowData.allTournaments && rowData.allTournaments.length > 0) {
                    canApply = rowData.allTournaments.some(t => t.tournament.id === tournamentId);
                }
            } else {
                // No tournament selected - can always apply
                canApply = true;
            }

            if (!canApply) {
                skippedCount++;
                return; // Skip this cell - selected tournament not on this date
            }

            // Create absence entry
            const cellData = {
                notWorking: true,
                entryType: selectedEntryType,  // 3=Sick, 4=TimeOffOffsite, 5=TimeOffOnsite
                tournamentId: tournamentId,
                tournament: tournamentId ? this.tournaments.find(t => t.id === tournamentId)?.name : null,
                assigned: false,
                travel: false,
                isTravel: false,
                role: null,
                dailyRate: null,
                dailyRate2: null
            };

            // Update the cell data
            this.data[cellId] = cellData;

            // Track the change
            if (typeof this.trackCellChange === 'function') {
                this.trackCellChange(rowIndex, colIndex, cellData);
            }

            // Refresh the cell display - pass jQuery object
            if (typeof this.updateCellDisplay === 'function') {
                this.updateCellDisplay($cell, cellData);
            }

            processedCount++;
        });

        // Save changes
        if (processedCount > 0) {
            this.saveToStorage(true); // Force immediate save
        }

        // Show feedback
        if (processedCount > 0) {
            const entryTypeName = selectedEntryType === 3 ? 'Sick' :
                selectedEntryType === 4 ? 'Time Off - Offsite' : 'Time Off - Onsite';
            let message = `Marked ${processedCount} cell(s) as ${entryTypeName}`;
            if (skippedCount > 0) {
                message += ` (${skippedCount} skipped - tournament not on date)`;
            }
            this.showUserFeedback(message, 'success');
        }

        // Close the modal
        $('#absence-type-modal').modal('hide');
    }

    performNotWorkingAssignment(cell, tournament) {
        // Create the cell content
        const cellContent = `
      <div class="tournament-info">${tournament.name}</div>
      <div class="assignment-status">Not Working</div>
    `;

        // Update the cell
        cell.removeClass("assigned").addClass("not-working");
        cell.find(".cell-display").html(cellContent);

        // Not Working cells use default styling (no tournament colors)

        // Store the assignment in the data structure using row/col indices
        const rowIndex = parseInt(cell.data("row"));
        const colIndex = parseInt(cell.data("col"));

        // Preserve existing style data if present
        const cellId = `${colIndex}_${rowIndex}`;
        const existingStyle = this.data[cellId]?.style || {};

        const cellData = {
            value: `${tournament.name} - Not Working`,
            tournament: tournament.id,
            tournamentId: tournament.id,
            assigned: false,
            isWorking: false,
            notWorking: true,
            travel: false,
            style: existingStyle,
        };

        this.data[cellId] = cellData;

        // Track this change for incremental saving
        if (this.trackCellChange) {
            this.trackCellChange(rowIndex, colIndex, cellData);
        }
    }

    performStandaloneNotWorkingAssignment(cell) {
        // Create the cell content for standalone not working day
        const cellContent = `
            <div class="assignment-status">Not Working</div>
        `;

        // Update the cell
        cell.removeClass("assigned travel").addClass("not-working");
        cell.find(".cell-display").html(cellContent);

        // Store the assignment in the data structure using row/col indices
        const rowIndex = parseInt(cell.data("row"));
        const colIndex = parseInt(cell.data("col"));

        // Preserve existing style data if present
        const cellId = `${colIndex}_${rowIndex}`;
        const existingStyle = this.data[cellId]?.style || {};

        const cellData = {
            value: "Not Working",
            assigned: false,
            isWorking: false,
            notWorking: true,
            travel: false,
            style: existingStyle,
        };

        this.data[cellId] = cellData;

        // Track this change for incremental saving
        if (this.trackCellChange) {
            this.trackCellChange(rowIndex, colIndex, cellData);
        }
    }

    showTournamentSelectionForNotWorking(cells) {
        // Get all unique tournaments from the selected cells
        const availableTournaments = new Map();

        cells.forEach((cell) => {
            const row = parseInt(cell.data("row"));
            const rowData = this.scheduleRows[row];

            if (rowData && rowData.allTournaments) {
                rowData.allTournaments.forEach((activity) => {
                    const tournamentId = activity.tournament.id;
                    if (!availableTournaments.has(tournamentId)) {
                        availableTournaments.set(tournamentId, {
                            tournament: activity.tournament,
                            cells: [cell]
                        });
                    } else {
                        availableTournaments.get(tournamentId).cells.push(cell);
                    }
                });
            }
        });

        // Populate the selection modal
        const listContainer = $("#tournament-selection-list");
        listContainer.empty();

        availableTournaments.forEach((data, tournamentId) => {
            const button = $(`
        <button type="button" class="btn btn-outline-primary d-block w-100 mb-2 tournament-selection-btn"
                data-tournament-id="${data.tournament.id}">
            <strong>${data.tournament.name}</strong><br>
            <small>Mark ${data.cells.length} cells as "Not Working"</small>
        </button>
      `);

            button.click(() => {
                // Mark all relevant cells as not working for this tournament
                cells.forEach((cell) => {
                    const row = parseInt(cell.data("row"));
                    const rowData = this.scheduleRows[row];

                    if (rowData && rowData.allTournaments) {
                        const hasThisTournament = rowData.allTournaments.some(
                            activity => activity.tournament.id === data.tournament.id
                        );

                        if (hasThisTournament) {
                            this.performNotWorkingAssignment(cell, data.tournament);
                        }
                    }
                });

                this.saveToStorage();

                const modal = bootstrap.Modal.getInstance("#tournamentSelectionModal");
                modal.hide();
            });

            listContainer.append(button);
        });

        // Update modal title
        $("#tournamentSelectionModalLabel").text("Select Tournament for Not Working");

        // Show the modal
        const modal = new bootstrap.Modal("#tournamentSelectionModal");
        modal.show();
    }

    markTravel() {
        if (this.selectedCells.length === 0) return;

        // Open the travel modal instead of directly marking
        this.openTravelModal();
    }

    openTravelModal() {
        if (!this.selectedCells || this.selectedCells.length === 0) {
            alert('Please select cells first.');
            return;
        }

        // Use all tournaments for travel day selection (not just overlapping ones)
        const availableTournaments = this.tournaments.map(t => ({
            id: t.id,
            name: t.name,
            travelPerDiem: t.travelPerDiem || 0
        }));

        // ORIGINAL: Only gather tournaments that overlap with selected cells
        // const tournamentMap = new Map();
        //
        // this.selectedCells.forEach((cell) => {
        //     const rowIndex = parseInt(cell.data("row"));
        //     const rowData = this.scheduleRows[rowIndex];
        //
        //     if (rowData && rowData.allTournaments && rowData.allTournaments.length > 0) {
        //         rowData.allTournaments.forEach(t => {
        //             if (!tournamentMap.has(t.tournament.id)) {
        //                 // Find full tournament data from this.tournaments
        //                 const fullTournament = this.tournaments.find(tour => tour.id === t.tournament.id);
        //                 tournamentMap.set(t.tournament.id, {
        //                     id: t.tournament.id,
        //                     name: t.tournament.name,
        //                     travelPerDiem: fullTournament?.travelPerDiem || 0
        //                 });
        //             }
        //         });
        //     }
        // });
        //
        // // Convert tournament map to array
        // const availableTournaments = Array.from(tournamentMap.values());

        // Populate both "From" and "To" dropdowns
        const fromSelect = $('#travel-from');
        const toSelect = $('#travel-to');

        fromSelect.empty();
        toSelect.empty();

        // Add "Home" as first option for both dropdowns
        fromSelect.append('<option value="0">Home</option>');
        toSelect.append('<option value="0">Home</option>');

        // Add tournaments to both dropdowns
        availableTournaments.forEach(t => {
            fromSelect.append(`<option value="${t.id}" data-perdiem="${t.travelPerDiem}">${t.name}</option>`);
            toSelect.append(`<option value="${t.id}" data-perdiem="${t.travelPerDiem}">${t.name}</option>`);
        });

        // Store available tournaments for later reference
        this.availableTravelTournaments = availableTournaments;

        // Reset validation and summary
        $('#travel-validation-error').hide();
        $('#travel-summary').hide();

        // Setup change handlers for dropdowns to show live preview
        const self = this;
        $('#travel-from, #travel-to').off('change').on('change', function() {
            self.updateTravelSummary();
        });

        // Setup confirm button handler
        $('#confirm-travel').off('click').on('click', function() {
            self.handleTravelConfirm();
        });

        // Show the modal
        $('#travel-modal').modal('show');
    }

    updateTravelSummary() {
        const fromValue = $('#travel-from').val();
        const toValue = $('#travel-to').val();

        if (!fromValue || !toValue) {
            $('#travel-summary').hide();
            $('#travel-validation-error').hide();
            return;
        }

        // Validation: Cannot travel from and to the same location
        if (fromValue === toValue) {
            $('#travel-validation-message').text('You cannot travel from and to the same location.');
            $('#travel-validation-error').show();
            $('#travel-summary').hide();
            return;
        }

        $('#travel-validation-error').hide();

        // Get location names
        const fromText = fromValue === "0" ? "Home" :
            $('#travel-from option:selected').text();
        const toText = toValue === "0" ? "Home" :
            $('#travel-to option:selected').text();

        // Calculate per diem rate based on scenario
        let perDiemRate = 0;
        let perDiemExplanation = "";

        if (fromValue === "0" || toValue === "0") {
            // Home ↔ Tournament: Use tournament's TravelPerDiem
            const tournamentSelect = fromValue === "0" ? $('#travel-to') : $('#travel-from');
            perDiemRate = parseFloat(tournamentSelect.find('option:selected').data('perdiem')) || 0;
            perDiemExplanation = `Using tournament's travel per diem rate`;
        } else {
            // Tournament ↔ Tournament: Average both
            const fromPerDiem = parseFloat($('#travel-from option:selected').data('perdiem')) || 0;
            const toPerDiem = parseFloat($('#travel-to option:selected').data('perdiem')) || 0;
            perDiemRate = (fromPerDiem + toPerDiem) / 2;
            perDiemExplanation = `Average of both tournaments ($${fromPerDiem.toFixed(2)} + $${toPerDiem.toFixed(2)}) / 2`;
        }

        // Display summary
        $('#travel-summary-text').text(`${fromText} → ${toText}`);
        $('#travel-perdiem-text').html(`$${perDiemRate.toFixed(2)}<br><small class="text-muted">${perDiemExplanation}</small>`);
        $('#travel-summary').show();
    }

    handleTravelConfirm() {
        const fromValue = $('#travel-from').val();
        const toValue = $('#travel-to').val();

        // Validation
        if (!fromValue || !toValue) {
            alert('Please select both "From" and "To" locations');
            return;
        }

        if (fromValue === toValue) {
            alert('You cannot travel from and to the same location');
            return;
        }

        // Determine which tournaments to store based on selection
        let tournamentId = null;
        let travelTournamentId = null;

        if (fromValue === "0" || toValue === "0") {
            // Home ↔ Tournament: Store tournament in tournamentId, travelTournamentId = null
            tournamentId = fromValue === "0" ? parseInt(toValue) : parseInt(fromValue);
            travelTournamentId = null;
        } else {
            // Tournament ↔ Tournament: Store from in tournamentId, to in travelTournamentId
            tournamentId = parseInt(fromValue);
            travelTournamentId = parseInt(toValue);
        }

        let processedCount = 0;

        // Process each selected cell
        this.selectedCells.forEach($cell => {
            const rowIndex = parseInt($cell.data('row'));
            const colIndex = parseInt($cell.data('col'));
            const cellId = `${colIndex}_${rowIndex}`;

            // Create travel entry with tournament IDs
            const cellData = {
                travel: true,
                isTravel: true,
                entryType: 2,  // EntryType 2 = Travel
                tournamentId: tournamentId,
                travelTournamentId: travelTournamentId,
                tournament: null,
                assigned: false,
                notWorking: false,
                role: null,
                dailyRate: null,
                dailyRate2: null,
                value: "Travel"
            };

            // Update the cell data
            this.data[cellId] = cellData;

            // Track the change
            if (typeof this.trackCellChange === 'function') {
                this.trackCellChange(rowIndex, colIndex, cellData);
            }

            // Refresh the cell display
            if (typeof this.updateCellDisplay === 'function') {
                this.updateCellDisplay($cell, cellData);
            } else {
                // Fallback display update
                const cellContent = `<div class="assignment-status">Travel</div>`;
                $cell.removeClass("assigned not-working").addClass("travel");
                $cell.find(".cell-display").html(cellContent);
            }

            processedCount++;
        });

        // Save changes
        if (processedCount > 0) {
            this.saveToStorage(true); // Force immediate save
            this.showUserFeedback(`Marked ${processedCount} cell(s) as Travel Day`, 'success');
        }

        // Close the modal
        $('#travel-modal').modal('hide');
    }

    clearAllSelections() {
        this.selectedCells.forEach((cell) => {
            cell.removeClass("selected");
        });
        this.selectedCells = [];
        // Remove any date cell highlights
        $('.row-header.date-highlighted').removeClass('date-highlighted');
        // Update selected cell count
        this.updateSelectedCellCount();
    }

    highlightDateCells(rowIndices) {
        // Remove previous date highlights
        $('.row-header.date-highlighted').removeClass('date-highlighted');
        // Highlight date cells for the given rows
        rowIndices.forEach(rowIndex => {
            const dateCell = $(`.row-header[data-row="${rowIndex}"]`);
            if (dateCell.length) {
                dateCell.addClass('date-highlighted');
            }
        });
    }

    toggleCellSelection(cell) {
        const index = this.selectedCells.indexOf(cell);
        if (index > -1) {
            // Cell is already selected, remove it
            this.selectedCells.splice(index, 1);
            cell.removeClass("selected");
        } else {
            // Cell is not selected, add it
            this.selectedCells.push(cell);
            cell.addClass("selected");
            this.selectedCell = cell; // Keep last selected as primary
        }
        // Update selected cell count
        this.updateSelectedCellCount();
    }

    selectRange(startCell, endCell) {
        this.clearAllSelections();

        const startRow = parseInt(startCell.data("row"));
        const startCol = parseInt(startCell.data("col"));
        const endRow = parseInt(endCell.data("row"));
        const endCol = parseInt(endCell.data("col"));

        const minRow = Math.min(startRow, endRow);
        const maxRow = Math.max(startRow, endRow);
        const minCol = Math.min(startCol, endCol);
        const maxCol = Math.max(startCol, endCol);

        for (let row = minRow; row <= maxRow; row++) {
            for (let col = minCol; col <= maxCol; col++) {
                const cell = $(`.cell[data-row="${row}"][data-col="${col}"]`);
                if (cell.length) {
                    this.selectedCells.push(cell);
                    cell.addClass("selected");
                }
            }
        }

        // Highlight date cells for all rows in the selection range
        const rowIndices = [];
        for (let row = minRow; row <= maxRow; row++) {
            rowIndices.push(row);
        }
        this.highlightDateCells(rowIndices);

        this.selectedCell = endCell;

        // Update selected cell count
        this.updateSelectedCellCount();
    }

    updateSelectionFromPoint(clientX, clientY) {
        // Find the cell at the given screen coordinates
        const element = document.elementFromPoint(clientX, clientY);
        let cellElement = element ? $(element).closest('.cell') : $();

        if (cellElement.length === 0) {
            // Mouse is outside cells - find the best cell based on position
            const container = $(".spreadsheet-container")[0];
            if (!container) return;

            const containerRect = container.getBoundingClientRect();

            // Determine which edge(s) the mouse is near/beyond
            const beyondBottom = clientY > containerRect.bottom;
            const beyondTop = clientY < containerRect.top;
            const beyondRight = clientX > containerRect.right;
            const beyondLeft = clientX < containerRect.left;

            // Find the best cell - prioritize cells at the edges in the drag direction
            let bestCell = null;
            let bestScore = -Infinity;

            $('.cell[data-row][data-col]').each(function() {
                const $cell = $(this);
                const row = parseInt($cell.data('row'));
                const col = parseInt($cell.data('col'));
                const cellRect = this.getBoundingClientRect();

                let score = 0;

                // Score based on vertical position
                if (beyondBottom) {
                    // Prefer cells with higher row numbers (further down)
                    score += row * 1000;
                } else if (beyondTop) {
                    // Prefer cells with lower row numbers (further up)
                    score -= row * 1000;
                } else {
                    // Mouse is within vertical bounds - prefer cells closer to mouse Y
                    score -= Math.abs(clientY - (cellRect.top + cellRect.height / 2));
                }

                // Score based on horizontal position
                if (beyondRight) {
                    // Prefer cells with higher column numbers (further right)
                    score += col * 100;
                } else if (beyondLeft) {
                    // Prefer cells with lower column numbers (further left)
                    score -= col * 100;
                } else {
                    // Mouse is within horizontal bounds - prefer cells closer to mouse X
                    score -= Math.abs(clientX - (cellRect.left + cellRect.width / 2)) * 0.1;
                }

                if (score > bestScore) {
                    bestScore = score;
                    bestCell = $cell;
                }
            });

            if (bestCell) {
                cellElement = bestCell;
            }
        }

        if (cellElement.length > 0 && this.selectionStart) {
            // Update selection end point only if it's different
            if (!this.selectionEnd || this.selectionEnd[0] !== cellElement[0]) {
                this.selectionEnd = cellElement;
                this.dragMoved = true;

                // Clear previous selections and select range
                this.clearAllSelections();
                this.selectRange(this.selectionStart, this.selectionEnd);
            }
        }
    }

    // Tournament filtering methods
    showTournamentFilterModal() {
        const modal = new bootstrap.Modal("#tournamentFilterModal");
        this.populateTournamentFilterList();
        modal.show();
    }

    populateTournamentFilterList() {
        const listContainer = $("#tournament-filter-list");
        listContainer.empty();

        // Filter tournaments to only show those with any activity in the current year (including phase dates)
        const currentYearTournaments = this.tournaments.filter(tournament =>
            this.isTournamentInYear(tournament, this.year)
        );

        if (currentYearTournaments.length === 0) {
            listContainer.append('<div class="text-muted">No tournaments found for this year</div>');
            return;
        }

        currentYearTournaments.forEach(tournament => {
            const isChecked = this.filteredTournamentIds.size === 0 || this.filteredTournamentIds.has(tournament.id);

            const checkbox = $(`
                <div class="form-check mb-2">
                    <input class="form-check-input tournament-filter-checkbox" type="checkbox"
                           value="${tournament.id}" id="tournament-${tournament.id}"
                           ${isChecked ? 'checked' : ''}>
                    <label class="form-check-label" for="tournament-${tournament.id}">
                        <strong>${tournament.name}</strong>
                        <br><small class="text-muted">${tournament.location} • ${tournament.startDate.toLocaleDateString()}</small>
                    </label>
                </div>
            `);

            listContainer.append(checkbox);
        });

        // Add event handlers for select all/deselect all
        const self = this;
        $("#select-all-tournaments").off('click').on('click', function() {
            $(".tournament-filter-checkbox").prop('checked', true);
        });

        $("#deselect-all-tournaments").off('click').on('click', function() {
            $(".tournament-filter-checkbox").prop('checked', false);
        });

        $("#apply-tournament-filter").off('click').on('click', function() {
            self.applyTournamentFilterFromModal();
        });
    }

    applyTournamentFilterFromModal() {
        const selectedTournamentIds = new Set();
        $(".tournament-filter-checkbox:checked").each(function() {
            selectedTournamentIds.add(parseInt($(this).val()));
        });

        this.filteredTournamentIds = selectedTournamentIds;
        this.updateFilterButtonText();

        // Call the existing filtering method
        this.applyTournamentFilter();

        // NOTE: "Assigned Only" toggle with tournament filter is currently disabled
        // Issue: Virtual scrolling doesn't properly re-render when column visibility changes
        // The second createGrid() call doesn't trigger _renderVisibleRows() execution
        // TODO: Fix virtual scrolling to handle column visibility changes, or refactor to avoid double rendering

        // Re-sort columns based on filtered tournament assignments
        this.sortColumnsByAssignmentCount();

        // COMMENTED OUT - Assigned Only integration
        // const wasShowingOnlyAssigned = this.showOnlyAssignedUsers;
        // if (wasShowingOnlyAssigned) {
        //     this.assignedUserColumns.clear();
        //     this.sortColumnsByAssignmentCount();
        //     this.assignedUserColumns = this.getAssignedUserColumns();
        //     this.virtualScrolling.renderedRange = { start: -1, end: -1 };
        //     this.createGrid();
        // }

        // Close the modal
        const modal = bootstrap.Modal.getInstance("#tournamentFilterModal");
        modal.hide();

        // Show feedback
        const filterCount = selectedTournamentIds.size;
        const totalTournaments = this.tournaments.filter(t =>
            this.isTournamentInYear(t, this.year)
        ).length;

        if (filterCount === 0 || filterCount === totalTournaments) {
            this.showUserFeedback('Showing all tournaments', 'info');
        } else {
            this.showUserFeedback(`Filtered to ${filterCount} tournament(s)`, 'success');
        }
    }


    updateFilterButtonText() {
        const btn = $("#tournament-filter-btn");

        if (this.filteredTournamentIds.size === 0) {
            btn.text("🔍 Filter: All Tournaments");
        } else if (this.filteredTournamentIds.size === 1) {
            const tournament = this.tournaments.find(t => this.filteredTournamentIds.has(t.id));
            btn.text(`🔍 Filter: ${tournament ? tournament.name : '1 Tournament'}`);
        } else {
            btn.text(`🔍 Filter: ${this.filteredTournamentIds.size} Tournaments`);
        }
    }

    handleColumnContextAction(action) {
        if (!this.selectedColumn) return;

        const selectedColIndex = parseInt(this.selectedColumn.data("col"));
        const selectedStaffName = this.columnNames[selectedColIndex];

        switch (action) {
            case "copy-template-to-column":
                this.copyTemplateToColumn(selectedColIndex, selectedStaffName);
                break;
            case "copy-column-as-template":
                this.copyColumnAsTemplate(selectedColIndex, selectedStaffName);
                break;
            case "clear-entire-column":
                this.clearEntireColumn(selectedColIndex, selectedStaffName);
                break;
            default:
                console.warn("Unknown column context action:", action);
        }
    }

    copyTemplateToColumn(targetColIndex, targetStaffName) {
        // Show template selection modal
        this.showTemplateSelectionModal(targetColIndex, targetStaffName);
    }

    showTemplateSelectionModal(targetColIndex, targetStaffName) {
        // Find available template columns with data
        const templateOptions = [];
        this.columnNames.forEach((colName, colIndex) => {
            if (colName.startsWith('Template')) {
                // Check if this template has any data
                let hasData = false;
                for (let rowIndex = 0; rowIndex < this.scheduleRows.length; rowIndex++) {
                    const cellId = `${colIndex}_${rowIndex}`;
                    if (this.data[cellId] && (this.data[cellId].tournamentId || this.data[cellId].travel || this.data[cellId].notWorking)) {
                        hasData = true;
                        break;
                    }
                }

                if (hasData) {
                    templateOptions.push({
                        index: colIndex,
                        name: colName,
                        displayName: colName
                    });
                }
            }
        });

        if (templateOptions.length === 0) {
            this.showUserFeedback("No template schedules found with assignments", "warning");
            return;
        }

        // Create modal content
        const modalContent = `
            <div class="modal fade" id="templateSelectionModal" tabindex="-1">
                <div class="modal-dialog">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title">Select Template to Copy</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <p class="text-muted small mb-3">
                                Select which template schedule to copy to <strong>${targetStaffName}</strong>:
                            </p>
                            <div class="list-group">
                                ${templateOptions.map(template => `
                                    <button type="button" class="list-group-item list-group-item-action" 
                                            data-template-index="${template.index}" data-template-name="${template.name}">
                                        <strong>${template.displayName}</strong>
                                        <div class="text-muted small">Click to copy this template schedule</div>
                                    </button>
                                `).join('')}
                            </div>
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // Remove existing modal and add new one
        $("#templateSelectionModal").remove();
        $("body").append(modalContent);

        const modal = new bootstrap.Modal(document.getElementById('templateSelectionModal'));

        // Handle template selection
        $("#templateSelectionModal").on('click', '.list-group-item', (e) => {
            const templateIndex = parseInt($(e.currentTarget).data('template-index'));
            const templateName = $(e.currentTarget).data('template-name');

            modal.hide();
            this.performTemplateCopy(templateIndex, templateName, targetColIndex, targetStaffName);
        });

        modal.show();
    }

    performTemplateCopy(templateColIndex, templateName, targetColIndex, targetStaffName) {
        console.log(`Copying ${templateName} to ${targetStaffName}...`);

        let copiedCount = 0;
        let skippedCount = 0;

        // Iterate through all rows and copy template data to target column
        for (let rowIndex = 0; rowIndex < this.scheduleRows.length; rowIndex++) {
            const templateCellId = `${templateColIndex}_${rowIndex}`;
            const targetCellId = `${targetColIndex}_${rowIndex}`;
            const templateData = this.data[templateCellId];

            // Skip if template cell has no data
            if (!templateData || (!templateData.tournamentId && !templateData.travel && !templateData.notWorking)) {
                continue;
            }

            // Check if target row has tournament activity (for tournament assignments)
            const scheduleRow = this.scheduleRows[rowIndex];
            const hasTournamentActivity = scheduleRow.tournament || (scheduleRow.allTournaments && scheduleRow.allTournaments.length > 0);

            // Copy template data
            if (templateData.tournamentId && !hasTournamentActivity) {
                // Skip tournament assignments where no tournament is scheduled
                skippedCount++;
                continue;
            }

            // Calculate proper phase for tournament assignments in the target context
            let updatedPhase = templateData.phase;
            if (templateData.tournamentId && hasTournamentActivity) {
                // Recalculate phase based on the target row's tournament context
                const scheduleRow = this.scheduleRows[rowIndex];
                if (scheduleRow.allTournaments) {
                    const activity = scheduleRow.allTournaments.find(act => act.tournament.id === templateData.tournamentId);
                    if (activity && activity.phase) {
                        updatedPhase = activity.phase;
                    }
                } else if (scheduleRow.tournament && scheduleRow.tournament.id === templateData.tournamentId) {
                    updatedPhase = scheduleRow.phase;
                }
            }

            // Create a copy of the template data
            this.data[targetCellId] = {
                ...templateData,
                phase: updatedPhase, // Use recalculated or original phase
                assigned: true
            };

            // Track this change for database saving (exclude template columns from database saves)
            if (this.trackCellChange) {
                this.trackCellChange(rowIndex, targetColIndex, this.data[targetCellId]);
            }

            // Update the visual display of the target cell
            const $targetCell = $(`.cell[data-row="${rowIndex}"][data-col="${targetColIndex}"]`);
            if ($targetCell.length > 0) {
                this.updateCellDisplay($targetCell, this.data[targetCellId]);
            }

            copiedCount++;
        }

        // Save changes to server (will automatically exclude template columns)
        this.saveToStorage(true);

        // Show feedback
        const message = skippedCount > 0
            ? `Copied ${copiedCount} assignments from ${templateName} to ${targetStaffName}. ${skippedCount} assignments skipped (no tournament scheduled).`
            : `Successfully copied ${copiedCount} assignments from ${templateName} to ${targetStaffName}.`;

        this.showUserFeedback(message, "success");
        console.log(`Template copy complete: ${copiedCount} copied, ${skippedCount} skipped`);
    }

    copyColumnAsTemplate(sourceColIndex, sourceStaffName) {
        // Show template destination selection modal
        this.showTemplateDestinationModal(sourceColIndex, sourceStaffName);
    }

    showTemplateDestinationModal(sourceColIndex, sourceStaffName) {
        // Find available template columns
        const templateOptions = [];
        this.columnNames.forEach((colName, colIndex) => {
            if (colName.startsWith('Template')) {
                // Check if this template has existing data
                let hasData = false;
                let assignmentCount = 0;
                for (let rowIndex = 0; rowIndex < this.scheduleRows.length; rowIndex++) {
                    const cellId = `${colIndex}_${rowIndex}`;
                    if (this.data[cellId] && (this.data[cellId].tournamentId || this.data[cellId].travel || this.data[cellId].notWorking)) {
                        hasData = true;
                        assignmentCount++;
                    }
                }

                templateOptions.push({
                    index: colIndex,
                    name: colName,
                    displayName: colName,
                    hasData: hasData,
                    assignmentCount: assignmentCount
                });
            }
        });

        if (templateOptions.length === 0) {
            this.showUserFeedback("No template columns available", "warning");
            return;
        }

        // Create modal content
        const modalContent = `
            <div class="modal fade" id="templateDestinationModal" tabindex="-1">
                <div class="modal-dialog">
                    <div class="modal-content">
                        <div class="modal-header">
                            <h5 class="modal-title">Copy to Template</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            <p class="text-muted small mb-3">
                                Select which template to copy <strong>${sourceStaffName}</strong>'s schedule to:
                            </p>
                            <div class="list-group">
                                ${templateOptions.map(template => `
                                    <button type="button" class="list-group-item list-group-item-action" 
                                            data-template-index="${template.index}" data-template-name="${template.name}">
                                        <div class="d-flex justify-content-between align-items-start">
                                            <div>
                                                <strong>${template.displayName}</strong>
                                                <div class="text-muted small">
                                                    ${template.hasData ? `${template.assignmentCount} existing assignments - will be overwritten` : 'Empty template'}
                                                </div>
                                            </div>
                                            ${template.hasData ? '<span class="badge bg-warning">Has Data</span>' : '<span class="badge bg-success">Empty</span>'}
                                        </div>
                                    </button>
                                `).join('')}
                            </div>
                            ${templateOptions.some(t => t.hasData) ? `
                                <div class="alert alert-warning mt-3">
                                    <i class="bi bi-exclamation-triangle"></i>
                                    <small>Templates marked "Has Data" will have their existing assignments overwritten.</small>
                                </div>
                            ` : ''}
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                        </div>
                    </div>
                </div>
            </div>
        `;

        // Remove existing modal and add new one
        $("#templateDestinationModal").remove();
        $("body").append(modalContent);

        const modal = new bootstrap.Modal(document.getElementById('templateDestinationModal'));

        // Handle template selection
        $("#templateDestinationModal").on('click', '.list-group-item', (e) => {
            const templateIndex = parseInt($(e.currentTarget).data('template-index'));
            const templateName = $(e.currentTarget).data('template-name');

            modal.hide();
            this.performUserToTemplateCopy(sourceColIndex, sourceStaffName, templateIndex, templateName);
        });

        modal.show();
    }

    performUserToTemplateCopy(sourceColIndex, sourceStaffName, templateColIndex, templateName) {
        console.log(`Copying ${sourceStaffName} schedule to ${templateName}...`);

        let copiedCount = 0;
        let clearedCount = 0;

        // First, clear existing template data
        for (let rowIndex = 0; rowIndex < this.scheduleRows.length; rowIndex++) {
            const templateCellId = `${templateColIndex}_${rowIndex}`;
            if (this.data[templateCellId]) {
                delete this.data[templateCellId];
                clearedCount++;
            }
        }

        // Copy user data to template
        for (let rowIndex = 0; rowIndex < this.scheduleRows.length; rowIndex++) {
            const sourceCellId = `${sourceColIndex}_${rowIndex}`;
            const templateCellId = `${templateColIndex}_${rowIndex}`;
            const sourceData = this.data[sourceCellId];

            // Skip if source cell has no data
            if (!sourceData || (!sourceData.tournamentId && !sourceData.travel && !sourceData.notWorking)) {
                continue;
            }

            // Create a copy of the source data for the template
            this.data[templateCellId] = {
                ...sourceData,
                assigned: true
            };

            // Update the visual display of the template cell
            const $templateCell = $(`.cell[data-row="${rowIndex}"][data-col="${templateColIndex}"]`);
            if ($templateCell.length > 0) {
                this.updateCellDisplay($templateCell, this.data[templateCellId]);
            }

            copiedCount++;
        }

        // Save template data to localStorage (templates don't go to database)
        this.saveToStorage();

        // Show feedback
        const message = clearedCount > 0
            ? `Copied ${copiedCount} assignments from ${sourceStaffName} to ${templateName}. ${clearedCount} existing template assignments were cleared.`
            : `Successfully copied ${copiedCount} assignments from ${sourceStaffName} to ${templateName}.`;

        this.showUserFeedback(message, "success");
        console.log(`User to template copy complete: ${copiedCount} copied, ${clearedCount} cleared from template`);
    }

    clearEntireColumn(colIndex, staffName) {
        let clearedCount = 0;

        // Clear all assignments in this column
        for (let rowIndex = 0; rowIndex < this.scheduleRows.length; rowIndex++) {
            const cellId = `${colIndex}_${rowIndex}`;
            if (this.data[cellId] && (this.data[cellId].tournamentId || this.data[cellId].travel || this.data[cellId].notWorking)) {
                // Track deletion for database saving
                if (this.trackCellChange) {
                    this.trackCellChange(rowIndex, colIndex, null);
                }

                // Clear the data
                delete this.data[cellId];

                // Update visual display
                const $cell = $(`.cell[data-row="${rowIndex}"][data-col="${colIndex}"]`);
                if ($cell.length > 0) {
                    $cell.removeClass('assigned not-working travel')
                        .removeClass('role-manager role-printer role-general-laborer role-unassigned')
                        .text('');
                }

                clearedCount++;
            }
        }

        // Save changes to server
        this.saveToStorage(true);

        this.showUserFeedback(`Cleared ${clearedCount} assignments from ${staffName}`, "success");
        console.log(`Cleared ${clearedCount} assignments from column ${colIndex} (${staffName})`);
    }

    openClockTimesModal() {
        if (!this.selectedCells || this.selectedCells.length === 0) {
            alert('Please select assigned cells first.');
            return;
        }

        // Get affected assignments and tournaments
        const affectedAssignments = this.getAffectedAssignments();
        const tournaments = [...new Set(affectedAssignments.map(a =>
            a.tournamentId))];

        if (tournaments.length > 1) {
            // Multiple tournaments - show selection modal first
            this.showTournamentSelectionForClockTimes(tournaments, affectedAssignments);
        } else if (tournaments.length === 1) {
            // Single tournament - go directly to time entry
            this.showClockTimesModal(tournaments[0], affectedAssignments);
        } else {
            alert('No tournament assignments found in selected cells.');
        }
    }

    getAffectedAssignments(actionType = null) {
        const assignments = [];

        // Determine which data source to use (filteredData when filter is active, otherwise data)
        const dataSource = this.filteredData && this.filteredTournamentIds.size > 0 ? this.filteredData : this.data;

        this.selectedCells.forEach($cell => {
            const rowIndex = $cell.data('row');
            const colIndex = $cell.data('col');
            const cellId = `${colIndex}_${rowIndex}`;

            // Use existing data structure like other methods do
            const cellData = dataSource[cellId];
            const scheduleRow = this.scheduleRows[rowIndex];

            // For rate type changes, only include cells that have actual assignments
            if (actionType === 'change-rate-type') {
                // More strict filtering for rate type changes - must have assignment and tournament
                if (cellData && cellData.assigned &&
                    (cellData.tournamentId || cellData.tournament)) {
                    // Check if tournament exists in the selected cell

                    const tournamentId = cellData.tournamentId || cellData.tournament;
                    const tournament = this.tournaments.find(t => t.id === tournamentId);

                    if (tournament && scheduleRow) {
                        assignments.push({
                            userId: this.getUserIdFromColumnIndex(colIndex),
                            userName: this.columnNames[colIndex],
                            date: scheduleRow.date,
                            tournamentId: tournamentId,
                            tournamentName: tournament.name,
                            cellId: cellId,
                            rateType: cellData.rateType || (cellData.dailyRate2 ? 'daily' : 'salary'),
                            dailyRate2: cellData.dailyRate2 || null,
                            // billRate: cellData.billRate || null,
                            role: cellData.role || 'Unassigned',
                        });
                    }
                }
            } else {
                // Original logic for other actions (like clock times)
                if (cellData && (cellData.tournamentId || cellData.tournament) &&
                    cellData.assigned) {
                    const tournamentId = cellData.tournamentId || cellData.tournament;
                    const tournament = this.tournaments.find(t => t.id ===
                        tournamentId);

                    if (tournament && scheduleRow) {
                        assignments.push({
                            userId: this.getUserIdFromColumnIndex(colIndex),
                            userName: this.columnNames[colIndex],
                            date: scheduleRow.date,
                            tournamentId: tournamentId,
                            tournamentName: tournament.name,
                            cellId: cellId,
                            rateType: cellData.rateType || (cellData.dailyRate2 ? 'daily' : 'salary'),
                            dailyRate2: cellData.dailyRate2 || null,
                            // billRate: cellData.billRate || null,
                        });
                    }
                }
            }
        });

        return assignments;
    }

    getUserIdFromColumnIndex(colIndex) {
        // Get user ID from the EventStaffData based on column index
        if (window.EventStaffData && window.EventStaffData[colIndex]) {
            return window.EventStaffData[colIndex].UserId;
        }
        return null;
    }

    showTournamentSelectionForClockTimes(tournaments, assignments) {
        // Use the existing tournament selection modal (following established pattern)
        const modal = $("#tournamentSelectionModal");
        const listContainer = $("#tournament-selection-list");

        // Update modal title for clock times context
        $("#tournamentSelectionModalLabel").html('<i class="bi bi-clock"></i> Select Tournament for Clock Times');

        // Update modal description
        modal.find('.modal-body p').html(`
            <i class="bi bi-info-circle"></i>
            The selected cells span multiple tournaments. Please choose which tournament to set clock times for:
        `);

        // Clear and populate tournament list
        listContainer.empty();

        tournaments.forEach(tournamentId => {
            const tournament = this.tournaments.find(t => t.id === tournamentId);
            const assignmentCount = assignments.filter(a => a.tournamentId === tournamentId).length;

            if (tournament) {
                const button = $(`
                    <button type="button" class="list-group-item list-group-item-action d-flex justify-content-between align-items-center mb-2">
                        <div>
                            <strong>${tournament.name}</strong>
                            <br>
                            <small class="text-muted">${assignmentCount} assignments affected</small>
                        </div>
                        <i class="bi bi-chevron-right"></i>
                    </button>
                `);

                // Handle tournament selection (following existing pattern)
                button.click(() => {
                    const filteredAssignments = assignments.filter(a => a.tournamentId === tournamentId);

                    // Hide the modal
                    const modalInstance = bootstrap.Modal.getInstance(modal[0]);
                    if (modalInstance) {
                        modalInstance.hide();
                    } else {
                        modal.modal('hide');
                    }

                    // Show clock times modal for selected tournament
                    this.showClockTimesModal(tournamentId, filteredAssignments);
                });

                listContainer.append(button);
            }
        });

        // Show the modal (following existing pattern)
        const modalInstance = new bootstrap.Modal(modal[0]);
        modalInstance.show();
    }

    showClockTimesModal(tournamentId, assignments) {
        const tournament = this.tournaments.find(t => t.id === tournamentId);
        const modal = $('#clock-times-modal');

        // Update modal title info
        $('#selected-tournament-info').text(`Tournament: ${tournament.name}`);

        const dateRange = this.getDateRange(assignments.map(a => a.date));
        $('#affected-dates-info').text(`Dates: ${dateRange}`);

        // Group assignments by user
        const userGroups = this.groupAssignmentsByUser(assignments);

        // Populate staff time entries
        this.populateStaffTimeEntries(userGroups, tournamentId);

        // Setup bulk time application
        this.setupBulkTimeHandlers();

        // Setup save handler
        this.setupClockTimesSaveHandler(assignments);

        // Clear bulk time inputs
        this.clearBulkTimeInputs();

        modal.modal('show');
    }

    getDateRange(dates) {
        if (dates.length === 0) return '';
        if (dates.length === 1) return new Date(dates[0]).toLocaleDateString();

        const sortedDates = dates.sort((a, b) => new Date(a) - new Date(b));
        const startDate = new Date(sortedDates[0]).toLocaleDateString();
        const endDate = new Date(sortedDates[sortedDates.length -
            1]).toLocaleDateString();

        return `${startDate} - ${endDate}`;
    }

    groupAssignmentsByUser(assignments) {
        const groups = {};
        assignments.forEach(assignment => {
            if (!groups[assignment.userId]) {
                groups[assignment.userId] = {
                    userId: assignment.userId,
                    userName: assignment.userName,
                    dates: []
                };
            }
            groups[assignment.userId].dates.push(assignment.date);
        });
        return Object.values(groups);
    }

    populateStaffTimeEntries(userGroups, tournamentId) {
        const container = $('#staff-time-entries');
        container.empty();

        userGroups.forEach(user => {
            const userCard = $(`
              <div class="card mb-3" data-user-id="${user.userId}">
                  <div class="card-header">
                      <div class="form-check">
                          <input class="form-check-input staff-checkbox"
  type="checkbox"
                                 id="staff-${user.userId}"
  data-user-id="${user.userId}">
                          <label class="form-check-label"
  for="staff-${user.userId}">
                              <strong>${user.userName}</strong>
                          </label>
                      </div>
                  </div>
                  <div class="card-body">
                      <div class="time-entries" id="entries-${user.userId}">
                          <!-- Date entries will be added here -->
                      </div>
                  </div>
              </div>
          `);

            // Add date entries for each user
            const entriesContainer = userCard.find(`#entries-${user.userId}`);
            user.dates.forEach(date => {
                // Find existing clock times for this user/date/tournament
                let existingClockIn = "";
                let existingClockOut = "";

                // Look through all cell data to find existing times for this user and date
                for (const [cellId, cellData] of Object.entries(this.data)) {
                    if (cellData && cellData.tournamentId === tournamentId) {
                        const [colIndex, rowIndex] = cellId.split('_').map(Number);
                        const staffName = this.columnNames[colIndex];
                        const scheduleRow = this.scheduleRows[rowIndex];

                        // Check if this is the right user and date
                        if (staffName === user.userName && scheduleRow && scheduleRow.date) {
                            const cellDate = new Date(scheduleRow.date).toISOString().split('T')[0];
                            const targetDate = new Date(date).toISOString().split('T')[0];

                            if (cellDate === targetDate && cellData.hasClockTimes) {
                                // Convert existing clock times to time input format (HH:MM)

                                if (cellData.clockIn) {
                                    existingClockIn = this.convertTimeToInputFormat(cellData.clockIn);
                                }
                                if (cellData.clockOut) {
                                    existingClockOut = this.convertTimeToInputFormat(cellData.clockOut);
                                }
                                break;
                            }
                        }
                    }
                }

                const dateEntry = $(`
                  <div class="row mb-2 date-entry" data-date="${date}">
                      <div class="col-md-4">
                          <label class="form-label">${new
                        Date(date).toLocaleDateString()}</label>
                      </div>
                      <div class="col-md-4">
                          <input type="time" class="form-control clock-in-time"
                                 data-user-id="${user.userId}"
                                 data-tournament-id="${tournamentId}"
                                 data-date="${date}"
                                 value="${existingClockIn}"
                                 placeholder="Clock In">
                      </div>
                      <div class="col-md-4">
                          <input type="time" class="form-control clock-out-time"
                                 data-user-id="${user.userId}"
                                 data-tournament-id="${tournamentId}"
                                 data-date="${date}"
                                 value="${existingClockOut}"
                                 placeholder="Clock Out">
                      </div>
                  </div>
              `);
                entriesContainer.append(dateEntry);
            });

            container.append(userCard);
        });
    }

    setupBulkTimeHandlers() {
        // Apply bulk times button
        $('#apply-bulk-times').off('click').on('click', () => {
            const clockIn = $('#bulk-clock-in').val();
            const clockOut = $('#bulk-clock-out').val();

            if (!clockIn || !clockOut) {
                alert('Please enter both clock-in and clock-out times.');
                return;
            }

            // Apply to selected staff only
            $('.staff-checkbox:checked').each(function () {
                const userId = $(this).data('user-id');
                $(`.clock-in-time[data-user-id="${userId}"]`).val(clockIn);
                $(`.clock-out-time[data-user-id="${userId}"]`).val(clockOut);
            });
        });

        // Select all staff checkbox
        $('#select-all-staff').off('change').on('change', function () {
            $('.staff-checkbox').prop('checked', $(this).is(':checked'));
        });
    }

    setupClockTimesSaveHandler(assignments) {
        $('#save-clock-times').off('click').on('click', async () => {
            const timeUpdates = [];

            // Collect all time entries
            $('.date-entry').each(function () {
                const userId = $(this).find('.clock-in-time').data('user-id');
                const tournamentId = $(this).find('.clock-in-time').data('tournament-id');
                const clockIn = $(this).find('.clock-in-time').val();
                const clockOut = $(this).find('.clock-out-time').val();

                // Get the date
                const rawDate = $(this).data('date');
                const date = new Date(rawDate);


                if (clockIn || clockOut) {
                    timeUpdates.push({
                        UserId: userId,
                        TournamentId: tournamentId,
                        Date: date.toISOString(),
                        ClockInTime: clockIn || "",
                        ClockOutTime: clockOut || ""
                    });
                }
            });


            if (timeUpdates.length === 0) {
                alert('No time entries to save.');
                return;
            }

            // Validate times
            const invalidEntries = timeUpdates.filter(update => {
                if (update.ClockInTime && update.ClockOutTime) {
                    const clockIn = new Date('1970-01-01T' + update.ClockInTime + ':00');
                    const clockOut = new Date('1970-01-01T' + update.ClockOutTime + ':00');
                    return clockOut <= clockIn;
                }
                return false;
            });

            if (invalidEntries.length > 0) {
                alert('Clock-out time must be after clock-in time for all entries.');
                return;
            }

            // Save to backend
            try {
                const response = await fetch(ProcessUrl('Tournament/SaveClockTimes'), {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify(timeUpdates)
                });

                const result = await response.json();

                if (result.success) {
                    // Update cell data with new clock times
                    timeUpdates.forEach(update => {
                        // Find cells for this user and tournament by iterating through all cells
                        for (const [cellId, cellData] of Object.entries(this.data)) {
                            if (cellData && cellData.tournamentId === update.TournamentId) {
                                // Check if this cell belongs to the correct user
                                const [colIndex, rowIndex] = cellId.split('_').map(Number);
                                const staffName = this.columnNames[colIndex];
                                const scheduleRow = this.scheduleRows[rowIndex];

                                // Find the user by ID to get their name
                                const staffMember = window.EventStaffData?.find(s => s.UserId === update.UserId);

                                if (staffMember && staffMember.FullName === staffName && scheduleRow && scheduleRow.date) {
                                    // Check if this is the correct DATE as well
                                    const cellDate = new Date(scheduleRow.date).toISOString().split('T')[0];
                                    const updateDate = new Date(update.Date).toISOString().split('T')[0];

                                    if (cellDate === updateDate) {
                                        // Update the cell data only for the matching date
                                        cellData.clockIn = update.ClockInTime || "";
                                        cellData.clockOut = update.ClockOutTime || "";
                                        cellData.hasClockTimes = !!(update.ClockInTime || update.ClockOutTime);

                                        // Update visual indicator
                                        this.updateClockIndicator(rowIndex, colIndex);

                                    }
                                }
                            }
                        }
                    });

                    alert('Clock times saved successfully!');
                    $('#clock-times-modal').modal('hide');
                } else {
                    alert('Error saving clock times: ' + result.message);
                }
            } catch (error) {
                console.error('Save error:', error);
                alert('Error saving clock times: ' + error.message);
            }
        });
    }

    updateClockIndicator(rowIndex, colIndex) {
        const cellId = `${colIndex}_${rowIndex}`;
        const cellData = this.data[cellId];
        const cell = $(`.cell[data-row="${rowIndex}"][data-col="${colIndex}"]`);

        if (cellData && cellData.hasClockTimes) {
            this.showClockIndicator(cell, cellData.clockIn, cellData.clockOut);
        } else {
            this.hideClockIndicator(cell);
        }
    }

    showClockIndicator(cell, clockIn, clockOut) {
        // Remove existing indicator if any
        cell.find('.clock-indicator').remove();

        // Create clock indicator
        const clockIcon = $(`
            <div class="clock-indicator" title="Clock Times: ${clockIn} - ${clockOut}">
                <i class="fas fa-clock"></i>
            </div>
        `);

        // Add indicator to cell
        cell.append(clockIcon);
    }

    hideClockIndicator(cell) {
        cell.find('.clock-indicator').remove();
    }

    refreshAllClockIndicators() {
        // Update all cells with clock indicators
        for (const [cellId, cellData] of Object.entries(this.data)) {
            if (cellData && cellData.assigned) {
                const [colIndex, rowIndex] = cellId.split('_').map(Number);
                this.updateClockIndicator(rowIndex, colIndex);
            }
        }
    }

    clearBulkTimeInputs() {
        // Clear the bulk time input fields when modal opens
        $('#bulk-clock-in').val('');
        $('#bulk-clock-out').val('');
    }

    convertTimeToInputFormat(timeString) {
        // Convert time strings like "3:00 AM" or "12:00 PM" to "03:00" or "12:00" format for time input
        if (!timeString) return "";


        try {
            // Create a date object with the time string
            const today = new Date().toISOString().split('T')[0]; // Get today's date
            const fullDateTimeString = `${today} ${timeString}`;

            const date = new Date(fullDateTimeString);

            // Return in HH:MM format
            const result = date.toTimeString().substr(0, 5);
            return result;
        } catch (error) {
            console.error('Error converting time:', timeString, error);
            return "";
        }
    }

    showAddUserModal() {
        // Reset the form
        $("#add-user-form")[0].reset();
        $("#add-user-form").removeClass("was-validated");

        // Show the modal
        const modal = new bootstrap.Modal("#addUserModal");
        modal.show();
    }

    async saveUser() {
        const form = $("#add-user-form")[0];
        const firstName = $("#user-first-name").val().trim();
        const lastName = $("#user-last-name").val().trim();

        // Validate form
        if (!firstName || !lastName) {
            form.classList.add("was-validated");
            return;
        }

        try {
            // Show loading state
            $("#save-user").prop("disabled", true).html('<i class="bi bi-hourglass-split"></i> Adding...');

            // Make API call to AddUser endpoint
            const response = await fetch(ProcessUrl('Tournament/AddUser'), {
                body: JSON.stringify({     // ✅ Use 'body' and stringify the JSON
                    first: firstName,
                    last: lastName
                }),
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                }
            });

            console.log("response", response);

            if (response.ok) {
                const newUserId = parseInt(await response.text());

                // Create new staff member object
                const newStaffMember = {
                    UserId: newUserId,
                    FirstName: firstName,
                    LastName: lastName,
                    FullName: `${firstName} ${lastName}`
                };

                // Add to EventStaffData array
                if (!window.EventStaffData) {
                    window.EventStaffData = [];
                }
                window.EventStaffData.push(newStaffMember);

                // Sort EventStaffData by first name, then last name to maintain order
                window.EventStaffData.sort((a, b) => {
                    if (a.FirstName !== b.FirstName) {
                        return a.FirstName.localeCompare(b.FirstName);
                    }
                    return a.LastName.localeCompare(b.LastName);
                });

                // Update columnNames to match EventStaffData
                this.columnNames = window.EventStaffData.map(staff => staff.FullName);

                // Refresh the grid to show the new user
                this.createGrid();

                // Show success message
                alert(`User "${firstName} ${lastName}" has been successfully added to the system.`);

                // Hide the modal
                const modal = bootstrap.Modal.getInstance("#addUserModal");
                modal.hide();

                // Reset form
                $("#add-user-form")[0].reset();
                $("#add-user-form").removeClass("was-validated");

            } else {
                const errorText = await response.text();
                console.error('Error response:', response.status, errorText);
                alert(`Error adding user (${response.status}): ${errorText}`);
            }
        } catch (error) {
            console.error('Error adding user:', error);
            alert(`Error adding user: ${error.message}`);
        } finally {
            // Reset button state
            $("#save-user").prop("disabled", false).html('<i class="bi bi-check-lg"></i> Add User');
        }
    }

    assignStaffFromAvailability(userId, staffName, date) {
        console.log("in assignStaffFromAvailability");
        // Store the context for the assignment workflow
        this.availabilityAssignmentContext = {
            userId: userId,
            staffName: staffName,
            date: date,
            tournamentId: null
        };

        // Close the Check Availability modal
        const availabilityModal = bootstrap.Modal.getInstance(document.getElementById('checkAvailabilityModal'));
        if (availabilityModal) {
            availabilityModal.hide();
        }

        // Show tournament selection modal
        this.showTournamentSelectionForAvailability(date);
    }

    showTournamentSelectionForAvailability(date) {
        const assignmentDate = new Date(date + 'T00:00:00');
        console.log("tournaments", this.tournaments)
        // Filter tournaments that are active on this date
        const activeTournaments = this.tournaments.filter(tournament => {
            // Handle both string and Date object formats
            const setupStart = tournament.setupStart instanceof Date
                ? tournament.setupStart
                : new Date(tournament.setupStart + 'T00:00:00');
            const teardownEnd = tournament.teardownEnd instanceof Date
                ? tournament.teardownEnd
                : new Date(tournament.teardownEnd + 'T00:00:00');
            console.log("setupStart", setupStart);
            console.log("teardownEnd", teardownEnd);
            return assignmentDate >= setupStart && assignmentDate <= teardownEnd;
        });

        if (activeTournaments.length === 0) {
            window.showError('No tournaments are active on this date.');
            return;
        }

        // Populate tournament selection list
        const list = $('#tournament-selection-list');
        list.empty();

        activeTournaments.forEach(tournament => {
            const button = $(`
                <button type="button" class="btn btn-outline-primary w-100 mb-2 tournament-selection-btn-availability"
                        data-tournament-id="${tournament.id}">
                    <div class="d-flex justify-content-between align-items-center">
                        <div>
                            <i class="bi bi-trophy"></i> ${tournament.name}
                        </div>
                        <small class="text-muted">${tournament.location}</small>
                    </div>
                </button>
            `);

            // Add click handler
            button.on('click', () => {
                this.onTournamentSelectedForAvailability(tournament.id);
            });

            list.append(button);
        });

        // Update modal title
        $('#tournamentSelectionModalLabel').text('Select Tournament for Assignment');

        // Show the modal
        const modal = new bootstrap.Modal(document.getElementById('tournamentSelectionModal'));
        modal.show();
    }

    onTournamentSelectedForAvailability(tournamentId) {
        // Store tournament ID in context
        this.availabilityAssignmentContext.tournamentId = tournamentId;

        // Hide tournament selection modal
        const tournamentModal = bootstrap.Modal.getInstance(document.getElementById('tournamentSelectionModal'));
        if (tournamentModal) {
            tournamentModal.hide();
        }

        // Show rate type modal
        this.showRateTypeModalForAvailability();
    }

    showRateTypeModalForAvailability() {
        const modal = $('#rate-type-modal');

        // Update modal title
        $('.modal-title', modal).text('Assignment Rate Type');

        // Get the selected tournament and display its info
        const tournamentId = this.availabilityAssignmentContext.tournamentId;
        const tournament = this.tournaments.find(t => t.id === tournamentId);
        if (tournament) {
            this.displayTournamentInfo(modal, tournament);
        }

        // Reset the rate type modal
        $('#salary-rate').prop('checked', true);
        $('#daily-rate-amount').val('');
        $('#daily-rate-input').hide();

        // Populate role dropdown
        const roleSelect = $('#assignment-role');
        roleSelect.empty();
        //roleSelect.append('<option value="Unassigned">Unassigned</option>');

        window.TournamentRoles.forEach(role => {
            roleSelect.append(`<option value="${role.Id}" data-rate="${role.DailyRate}">${role.Title} </option>`);
        });

        // Handle radio button changes to show/hide daily rate input
        $('input[name="rateType"]').off('change').on('change', function() {
            if ($(this).val() === 'daily') {
                $('#daily-rate-input').show();
                $('#daily-rate-amount').focus();
            } else {
                $('#daily-rate-input').hide();
            }
        });

        // Remove any existing event handlers to avoid duplicates
        $('#confirm-rate-type').off('click').on('click', () => {
            this.confirmRateTypeForAvailability();
        });

        // Show the modal using jQuery method for consistency
        modal.modal('show');
    }

    confirmRateTypeForAvailability() {
        const rateType = $('input[name="rateType"]:checked').val();
        const roleSelect = $('#assignment-role');
        const roleId = roleSelect.val();
        const roleName = roleSelect.find('option:selected').text();

        let dailyRate = null;
        let billRate = null;
        if (rateType === 'daily') {
            dailyRate = parseFloat($('#daily-rate-amount').val());
            if (!dailyRate || dailyRate <= 0) {
                window.showError('Please enter a valid daily rate amount.');
                return;
            }
            billRate = parseFloat($('#bill-rate-amount').val()) || null;
        }

        // Hide rate type modal
        const rateModal = bootstrap.Modal.getInstance(document.getElementById('rate-type-modal'));
        if (rateModal) {
            rateModal.hide();
        }

        // Remove the event handler
        $('#confirm-rate-type').off('click.availability');

        // Perform the assignment
        this.performAssignmentFromAvailability(rateType, roleId, roleName, dailyRate, billRate);
    }

    async performAssignmentFromAvailability(rateType, roleId, roleName, dailyRate, billRate) {
        const context = this.availabilityAssignmentContext;

        try {
            window.showLoading();

            // Find the role name and default rate from roleId
            let roleName = 'Unassigned';
            let roleDefaultRate = 0;
            if (roleId && roleId !== 'Unassigned') {
                const role = window.TournamentRoles.find(r => r.Id == roleId);
                if (role) {
                    roleName = role.Title;
                    roleDefaultRate = role.DailyRate || 0;
                }
            }

            // Prepare assignment data matching TournamentScheduleAssignment class
            const assignmentData = {
                UserId: context.userId,
                TournamentId: context.tournamentId,
                Date: context.date,
                Role: roleName,
                DailyRate: roleDefaultRate, // Role's default rate
                DailyRate2: dailyRate, // User-entered rate from the modal (can override)
                BillRate: billRate,
                IsTravel: false,
                IsAdvanceWeek: false,
                IsWorking: true,
                NotWorking: false
            };

            // Call SaveSchedule API - backend expects array directly, not wrapped
            const response = await fetch(ProcessUrl('Tournament/SaveSchedule'), {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify([assignmentData])
            });

            const result = await response.json();

            window.hideLoading();

            if (result.success) {
                window.showSuccess(`${context.staffName} has been assigned successfully.`);

                // Update the spreadsheet with the latest data from the server
                await this.loadScheduleData();

                // Apply the loaded assignments to the visual grid
                if (this.applyLoadedAssignmentsToGrid) {
                    this.applyLoadedAssignmentsToGrid();
                }

                // Refresh the availability results in the modal
                await this.refreshAvailabilityResults();
            } else {
                window.showError(result.message || 'Failed to assign staff member.');
            }
        } catch (error) {
            window.hideLoading();
            window.showError('An error occurred while assigning staff: ' + error.message);
        }
    }

    async refreshAvailabilityResults() {
        // Re-run the availability check with current date range
        const startDate = $('#availability-start-date').val();
        const endDate = $('#availability-end-date').val();

        if (startDate && endDate) {
            await window.checkStaffAvailability();
        }
    }

    // Open the report modal and populate tournament checkboxes
    openReportModal() {
        const container = $('#report-tournament-list');
        container.empty();

        // Filter tournaments for current year
        const currentYearTournaments = this.tournaments.filter(t => this.isTournamentInYear(t, this.year));

        if (currentYearTournaments.length === 0) {
            container.html('<p class="text-muted mb-0">No tournaments available for the current year.</p>');
        } else {
            currentYearTournaments.forEach(t => {
                const checkbox = $(`
                    <div class="form-check">
                        <input class="form-check-input report-tournament-checkbox" type="checkbox"
                               value="${t.id}" id="report-tournament-${t.id}">
                        <label class="form-check-label" for="report-tournament-${t.id}">
                            ${t.name} (${t.location || 'No location'})
                        </label>
                    </div>
                `);
                container.append(checkbox);
            });
        }

        // Reset results and staff selection
        $('#report-results').hide();
        $('#report-staff-selection').hide();
        $('#report-staff-list').empty();

        const modal = new bootstrap.Modal(document.getElementById('reportModal'));
        modal.show();
    }

    // Update staff selection based on selected tournaments
    updateStaffSelectionForReport() {
        const selectedIds = [];
        $('.report-tournament-checkbox:checked').each(function() {
            selectedIds.push(parseInt($(this).val()));
        });

        const staffContainer = $('#report-staff-list');
        staffContainer.empty();

        if (selectedIds.length === 0) {
            $('#report-staff-selection').hide();
            return;
        }

        // Find all staff assigned to selected tournaments
        const staffSet = new Set();
        const scheduleRowsToUse = this.allScheduleRows || this.scheduleRows;

        Object.entries(this.data).forEach(([cellId, cellData]) => {
            if (!cellData || !cellData.tournamentId) return;
            if (!selectedIds.includes(cellData.tournamentId)) return;

            const [colIndex] = cellId.split('_').map(Number);
            const staffName = this.columnNames[colIndex];
            if (staffName) {
                staffSet.add(staffName);
            }
        });

        if (staffSet.size === 0) {
            staffContainer.html('<p class="text-muted mb-0">No staff assigned to the selected tournaments.</p>');
            $('#report-staff-info').text('');
        } else {
            // Sort staff alphabetically and create checkboxes
            const sortedStaff = Array.from(staffSet).sort((a, b) => a.localeCompare(b));
            sortedStaff.forEach(staffName => {
                const safeName = staffName.replace(/[^a-zA-Z0-9]/g, '-');
                const checkbox = $(`
                    <div class="form-check">
                        <input class="form-check-input report-staff-checkbox" type="checkbox"
                               value="${staffName}" id="report-staff-${safeName}" checked>
                        <label class="form-check-label" for="report-staff-${safeName}">
                            ${staffName}
                        </label>
                    </div>
                `);
                staffContainer.append(checkbox);
            });
            $('#report-staff-info').text(`${staffSet.size} staff member(s) assigned to selected tournaments`);
        }

        $('#report-staff-selection').show();
    }

    // Generate the tournament assignment report
    generateReport() {
        // Get selected tournament IDs
        const selectedIds = [];
        $('.report-tournament-checkbox:checked').each(function() {
            selectedIds.push(parseInt($(this).val()));
        });

        if (selectedIds.length === 0) {
            alert('Please select at least one tournament');
            return;
        }

        // Get selected staff members
        const selectedStaff = new Set();
        $('.report-staff-checkbox:checked').each(function() {
            selectedStaff.add($(this).val());
        });

        if (selectedStaff.size === 0) {
            alert('Please select at least one staff member');
            return;
        }

        // Get selected tournaments
        const selectedTournaments = this.tournaments.filter(t => selectedIds.includes(t.id));

        // Calculate total tournament days (all phases - setup through teardown)
        let totalDays = 0;
        const tournamentDaysMap = new Map(); // tournamentId -> { name, days, start, end }

        selectedTournaments.forEach(t => {
            // Get the full range from setup start to teardown end
            const setupStart = t.setupStart ?
                (t.setupStart instanceof Date ? t.setupStart : new Date(t.setupStart)) :
                (() => { const d = new Date(t.startDate); d.setDate(d.getDate() - 21); return d; })();

            const teardownEnd = t.teardownEnd ?
                (t.teardownEnd instanceof Date ? t.teardownEnd : new Date(t.teardownEnd)) :
                (() => { const d = new Date(t.endDate); d.setDate(d.getDate() + 6); return d; })();

            const days = Math.ceil((teardownEnd - setupStart) / (1000 * 60 * 60 * 24)) + 1;
            totalDays += days;
            tournamentDaysMap.set(t.id, {
                name: t.name,
                days: days,
                start: setupStart,
                end: teardownEnd
            });
        });

        // Aggregate assignments by staff member
        // Structure: staffName -> { tournamentId -> { days: count, dates: [date strings] } }
        const staffAssignments = new Map();

        // Use allScheduleRows for consistent row indexing
        const scheduleRowsToUse = this.allScheduleRows || this.scheduleRows;

        Object.entries(this.data).forEach(([cellId, cellData]) => {
            if (!cellData || !cellData.tournamentId) return;
            if (!selectedIds.includes(cellData.tournamentId)) return;

            const [colIndex, rowIndex] = cellId.split('_').map(Number);
            const staffName = this.columnNames[colIndex];
            const scheduleRow = scheduleRowsToUse[rowIndex];

            if (!staffName || !scheduleRow) return;

            // Filter by selected staff
            if (!selectedStaff.has(staffName)) return;

            if (!staffAssignments.has(staffName)) {
                staffAssignments.set(staffName, new Map());
            }

            const staffData = staffAssignments.get(staffName);
            const tournamentId = cellData.tournamentId;

            if (!staffData.has(tournamentId)) {
                staffData.set(tournamentId, { days: 0, dates: [] });
            }

            const tournamentData = staffData.get(tournamentId);
            tournamentData.days++;
            tournamentData.dates.push(scheduleRow.date.toLocaleDateString());
        });

        // Update summary
        $('#report-tournament-count').text(selectedTournaments.length);
        $('#report-total-days').text(totalDays);
        $('#report-staff-count').text(staffAssignments.size);

        // Build detailed report HTML
        const detailsContainer = $('#report-details');
        detailsContainer.empty();

        if (staffAssignments.size === 0) {
            detailsContainer.html('<p class="text-muted text-center py-4"><i class="bi bi-info-circle"></i> No staff assignments found for the selected tournaments.</p>');
        } else {
            // Sort staff alphabetically
            const sortedStaff = Array.from(staffAssignments.entries()).sort((a, b) =>
                a[0].localeCompare(b[0])
            );

            sortedStaff.forEach(([staffName, tournaments]) => {
                let totalStaffDays = 0;
                tournaments.forEach(data => totalStaffDays += data.days);

                const card = $(`
                    <div class="card mb-2 report-staff-card">
                        <div class="card-header d-flex justify-content-between align-items-center">
                            <strong><i class="bi bi-person"></i> ${staffName}</strong>
                            <span class="badge bg-primary">${totalStaffDays} total days</span>
                        </div>
                        <div class="card-body p-2">
                            <table class="table table-sm table-striped mb-0">
                                <thead>
                                    <tr>
                                        <th>Tournament</th>
                                        <th>Days</th>
                                        <th>Dates</th>
                                    </tr>
                                </thead>
                                <tbody>
                                </tbody>
                            </table>
                        </div>
                    </div>
                `);

                const tbody = card.find('tbody');

                // Sort tournaments by name within each staff member
                const sortedTournaments = Array.from(tournaments.entries()).sort((a, b) => {
                    const nameA = tournamentDaysMap.get(a[0])?.name || '';
                    const nameB = tournamentDaysMap.get(b[0])?.name || '';
                    return nameA.localeCompare(nameB);
                });

                sortedTournaments.forEach(([tournamentId, data]) => {
                    const tournament = tournamentDaysMap.get(tournamentId);
                    const sortedDates = data.dates.sort((a, b) => new Date(a) - new Date(b));

                    // Show date range if more than 3 dates, otherwise show all
                    let dateDisplay;
                    if (sortedDates.length > 3) {
                        dateDisplay = `${sortedDates[0]} - ${sortedDates[sortedDates.length - 1]}`;
                    } else {
                        dateDisplay = sortedDates.join(', ');
                    }

                    tbody.append(`
                        <tr>
                            <td>${tournament?.name || 'Unknown'}</td>
                            <td><span class="badge bg-secondary">${data.days}</span></td>
                            <td><small>${dateDisplay}</small></td>
                        </tr>
                    `);
                });

                detailsContainer.append(card);
            });
        }

        // Show results
        $('#report-results').show();
    }

}
// Initialize the spreadsheet
let spreadsheetApp;
$(document).ready(() => {
    spreadsheetApp = new Spreadsheet();
    // Make it globally accessible for tournament management
    window.spreadsheetApp = spreadsheetApp;
});