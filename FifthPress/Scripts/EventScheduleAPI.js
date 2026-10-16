// EventScheduleAPI.js - Backend API Integration for Event Schedule
// This file connects the EventSchedule frontend to the Tournament backend API

(function () {
    'use strict';

    // Helper function to convert .NET date format to JavaScript Date
    function parseNetDate(dateString) {
        if (!dateString) return null;
        if (dateString.indexOf('/Date(') === 0) {
            const milliseconds = parseInt(dateString.replace('/Date(', '').replace(')/', ''));
            const utcDate = new Date(milliseconds);

            // Extract the UTC date components and create a local date
            // This preserves the intended date regardless of timezone
            const year = utcDate.getUTCFullYear();
            const month = utcDate.getUTCMonth();
            const day = utcDate.getUTCDate();
            const localDate = new Date(year, month, day);

            return localDate;
        }
        return new Date(dateString);
    }

    // Helper function to normalize a date to midnight (removes time component)
    function normalizeDate(date) {
        if (!date) return null;
        return new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
    }

    // Pre-calculate phase boundaries for a tournament (called once per tournament)
    function calculateTournamentPhaseBoundaries(tournament) {
        if (!tournament || !tournament.setupStart || !tournament.teardownEnd) {
            return null;
        }

        return {
            setupStart: normalizeDate(tournament.setupStart),
            setupEnd: normalizeDate(tournament.setupEnd || tournament.advancedStart) - (tournament.setupEnd ? 0 : 86400000),
            advancedStart: normalizeDate(tournament.advancedStart),
            advancedEnd: normalizeDate(tournament.advancedEnd || tournament.startDate) - (tournament.advancedEnd ? 0 : 86400000),
            tournamentStart: normalizeDate(tournament.startDate),
            tournamentEnd: normalizeDate(tournament.endDate),
            teardownStart: normalizeDate(tournament.teardownStart) || (normalizeDate(tournament.endDate) + 86400000),
            teardownEnd: normalizeDate(tournament.teardownEnd)
        };
    }

    // Determine phase for a given date using pre-calculated boundaries
    function getPhaseForDate(dateTimestamp, phaseBoundaries) {
        if (!phaseBoundaries) return null;

        if (dateTimestamp >= phaseBoundaries.setupStart && dateTimestamp <= phaseBoundaries.setupEnd) {
            return "setup";
        } else if (dateTimestamp >= phaseBoundaries.advancedStart && dateTimestamp <= phaseBoundaries.advancedEnd) {
            return "advanced";
        } else if (dateTimestamp >= phaseBoundaries.tournamentStart && dateTimestamp <= phaseBoundaries.tournamentEnd) {
            return "tournament";
        } else if (dateTimestamp >= phaseBoundaries.teardownStart && dateTimestamp <= phaseBoundaries.teardownEnd) {
            return "teardown";
        }
        return null;
    }

    // Wait for Spreadsheet to be defined, then override methods
    function overrideMethods() {
        if (window.Spreadsheet && Spreadsheet.prototype) {

            // Override the loadTournamentsFromDatabase method
            Spreadsheet.prototype.loadTournamentsFromDatabase = async function () {
                try {

                    // Get tournaments for the current year
                    const response = await fetch(ProcessUrl('Tournament/GetYearlyTournamentSchedule?year=' + this.year));

                    if (!response.ok) {
                        throw new Error(`HTTP error! status: ${response.status}`);
                    }

                    const data = await response.json();
                    
                    // Check if this is an error response
                    if (data.success === false) {
                        throw new Error(data.message);
                    }
                    

                    // The API returns tournaments array
                    const tournaments = data.Tournaments || [];


                    // Transform the backend data to match the frontend structure
                    this.tournaments = tournaments.map(t => ({
                        id: t.TournamentId,
                        name: t.TournamentName,
                        location: t.Location,
                        startDate: parseNetDate(t.StartDate),
                        endDate: parseNetDate(t.EndDate),
                        setupStart: parseNetDate(t.SetupStartDate),
                        setupEnd: parseNetDate(t.SetupEndDate),
                        advancedStart: parseNetDate(t.AdvanceWeekStartDate),
                        advancedEnd: parseNetDate(t.AdvanceWeekEndDate),
                        teardownStart: parseNetDate(t.TeardownStartDate),
                        teardownEnd: parseNetDate(t.TeardownEndDate),
                        status: t.Status,
                        clientId: t.ClientId,
                        clientName: t.ClientName,
                        minimumWage: t.MinimumWage || 0,
                        perDiem: t.PerDiem || 0,
                        travelPerDiem: t.TravelPerDiem || 0,
                        billRate: t.BillRate || null,
                        managerBillRate: t.ManagerBillRate || null,
                        printBillRate: t.PrintBillRate || null,
                        otByDay: t.OTByDay || false
                    }));

                    return this.tournaments;
                } catch (error) {
                    
                    // Fallback: try to use server-side data if API fails
                    if (window.TournamentsData && window.TournamentsData.length > 0) {
                        this.tournaments = window.TournamentsData.map(t => ({
                            id: t.Id,
                            name: t.Name,
                            location: t.Location,
                            startDate: parseNetDate(t.StartDate),
                            endDate: parseNetDate(t.EndDate),
                            setupStart: parseNetDate(t.SetupStartDate),
                            setupEnd: parseNetDate(t.SetupEndDate),
                            advancedStart: parseNetDate(t.AdvanceWeekStartDate),
                            advancedEnd: parseNetDate(t.AdvanceWeekEndDate),
                            teardownStart: parseNetDate(t.TeardownStartDate),
                            teardownEnd: parseNetDate(t.TeardownEndDate),
                            status: t.Status,
                            clientName: t.ClientName,
                            minimumWage: t.MinimumWage || 0,
                            perDiem: t.PerDiem || 0,
                            travelPerDiem: t.TravelPerDiem || 0,
                            billRate: t.BillRate || null,
                            managerBillRate: t.ManagerBillRate || null,
                            printBillRate: t.PrintBillRate || null,
                            otByDay: t.OTByDay || false
                        }));
                    } else {
                        this.tournaments = [];
                    }
                    return this.tournaments;
                }
            };


            // Override loadColumnsFromDatabase to load staff list from server-side data
            Spreadsheet.prototype.loadColumnsFromDatabase = async function () {
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
                        return;
                    }

                    // Fallback to API call if server-side data not available

                    const response = await fetch(ProcessUrl('Tournament/GetDailyStaffDistributionForFrontend?year=' + this.year));

                    if (!response.ok) {
                        throw new Error(`HTTP error! status: ${response.status}`);
                    }

                    const data = await response.json();

                    // Extract unique staff names from the distribution data
                    const staffSet = new Set();

                    if (data.StaffAssignments) {
                        data.StaffAssignments.forEach(assignment => {
                            staffSet.add(assignment.StaffName);
                        });
                    }

                    // Convert to array and sort alphabetically
                    const staffNames = Array.from(staffSet).sort();

                    if (staffNames.length > 0) {
                        this.columnNames = staffNames;
                    } else {
                        // Keep existing default columns if no staff found
                    }
                } catch (error) {
                    // Keep existing columns on error
                }
            };

            // Add method to save tournament to backend
            Spreadsheet.prototype.saveTournamentToBackend = async function (tournamentData) {
                try {

                    const payload = {
                        Name: tournamentData.name,
                        Description: tournamentData.name + ' Tournament',
                        Location: tournamentData.location,
                        StartDate: tournamentData.startDate,
                        EndDate: tournamentData.endDate,
                        SetupStartDate: tournamentData.setupStart,
                        SetupEndDate: tournamentData.setupEnd,
                        AdvanceWeekStartDate: tournamentData.advancedStart,
                        AdvanceWeekEndDate: tournamentData.advancedEnd,
                        TeardownStartDate: tournamentData.teardownStart,
                        TeardownEndDate: tournamentData.teardownEnd,
                        BillRate: tournamentData.billRate || null,
                        ManagerBillRate: tournamentData.managerBillRate || null,
                        PrintBillRate: tournamentData.printBillRate || null,
                        Status: 1, // Planned
                        ClientId: null // Will need to be set based on business logic
                    };

                    const response = await fetch(ProcessUrl('Tournament/CreateFromEventSchedule'), {
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

                    if (result.success) {
                        // Reload tournaments to get the new ID
                        await this.loadTournamentsFromDatabase();
                        return result.data;
                    } else {
                        throw new Error(result.message || 'Failed to save tournament');
                    }
                } catch (error) {
                    throw error;
                }
            };

            // Add method to update tournament in backend
            Spreadsheet.prototype.updateTournamentInBackend = async function (tournamentData) {
                try {

                    const payload = {
                        Id: tournamentData.id,
                        Name: tournamentData.name,
                        Description: tournamentData.name + ' Tournament',
                        Location: tournamentData.location,
                        StartDate: tournamentData.startDate,
                        EndDate: tournamentData.endDate,
                        SetupStartDate: tournamentData.setupStart,
                        SetupEndDate: tournamentData.setupEnd,
                        AdvanceWeekStartDate: tournamentData.advancedStart,
                        AdvanceWeekEndDate: tournamentData.advancedEnd,
                        TeardownStartDate: tournamentData.teardownStart,
                        TeardownEndDate: tournamentData.teardownEnd,
                        BillRate: tournamentData.billRate || null,
                        ManagerBillRate: tournamentData.managerBillRate || null,
                        PrintBillRate: tournamentData.printBillRate || null,
                        Status: tournamentData.status || 1
                    };

                    const response = await fetch(ProcessUrl('Tournament/EditFromEventSchedule'), {
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

                    if (result.success) {
                        return result.data;
                    } else {
                        throw new Error(result.message || 'Failed to update tournament');
                    }
                } catch (error) {
                    throw error;
                }
            };

            // Add method to delete tournament from backend
            Spreadsheet.prototype.deleteTournamentFromBackend = async function (tournamentId) {
                try {

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

                    if (result.success) {
                        return true;
                    } else {
                        throw new Error(result.message || 'Failed to delete tournament');
                    }
                } catch (error) {
                    throw error;
                }
            };

        } else {
            return false;
        }
        return true;
    }

    // Override window.spreadsheetApp assignment to hook into when it's created
    let originalSpreadsheetApp = null;

    Object.defineProperty(window, 'spreadsheetApp', {
        get: function () { return originalSpreadsheetApp; },
        set: function (value) {
            originalSpreadsheetApp = value;
            if (value) {
                overrideInstanceMethods(value);
                initializeWithServerData(value);
            }
        }
    });

    function overrideInstanceMethods(instance) {

        // CRITICAL: Override localStorage methods to prevent any local storage usage
        // Add intelligent saving that distinguishes between selection and data changes
        let saveTimeout = null;
        let lastDataSnapshot = null;
        let pendingChanges = new Map(); // Track specific cells that changed
        
        // Add method to track specific cell changes
        instance.trackCellChange = function (rowIndex, colIndex, cellData) {
            // Check if this column is a template column (not a real staff member)
            const staffName = this.columnNames[colIndex];
            const isTemplateColumn = staffName && staffName.startsWith('Template');

            // Note: Template columns now go to database too (no special handling needed)

            const cellKey = `${rowIndex}_${colIndex}`;
            pendingChanges.set(cellKey, { rowIndex, colIndex, cellData });
        };
        
        // Add method to clear tracked changes
        instance.clearTrackedChanges = function () {
            const changeCount = pendingChanges.size;
            pendingChanges.clear();
        };
        
        instance.saveToStorage = function (forceImmediate = false) {
            // Create a snapshot of current data
            const currentDataSnapshot = JSON.stringify(this.data);
            
            // Only save if data actually changed or if forced
            if (!forceImmediate && currentDataSnapshot === lastDataSnapshot) {
                return;
            }
            lastDataSnapshot = currentDataSnapshot;
            
            // Clear existing timeout
            if (saveTimeout) {
                clearTimeout(saveTimeout);
            }
            
            if (forceImmediate) {
                this.saveScheduleToServer();
                this.saveTemplateScheduleToServer();
            } else {
                // Debounce saves to avoid excessive server calls
                saveTimeout = setTimeout(() => {
                    this.saveScheduleToServer();
                    this.saveTemplateScheduleToServer();
                }, 500); // Wait 500ms after last change before saving
            }
        };

        instance.loadFromStorage = function () {
            // This method is no longer needed since templates are loaded from server
            // Keeping for compatibility but not using localStorage anymore
            return;
        };

        // Override loadColumnsFromDatabase on the instance
        instance.loadColumnsFromDatabase = async function () {
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
                    return;
                }

            } catch (error) {
                // Ignore loading errors for compatibility
            }
        };

        // Add loadTemplateData method 
        instance.loadTemplateData = async function () {
            try {
                const response = await fetch(ProcessUrl('Tournament/GetTemplateAssignments?year=' + this.year));

                if (!response.ok) {
                    throw new Error(`HTTP error! status: ${response.status}`);
                }

                const result = await response.json();

                if (result.success && result.assignments && result.assignments.length > 0) {

                    // Build lookup maps ONCE before processing (O(1) lookups instead of O(n) searches)
                    const templateNameToIndex = new Map();
                    this.columnNames.forEach((name, idx) => templateNameToIndex.set(name, idx));

                    const dateToRowIndex = new Map();
                    this.scheduleRows.forEach((row, idx) => {
                        const rowDate = row.date || row;
                        if (rowDate instanceof Date) {
                            dateToRowIndex.set(rowDate.toDateString(), idx);
                        }
                    });

                    // Pre-calculate phase boundaries for all tournaments ONCE
                    const tournamentPhases = new Map();
                    this.tournaments.forEach(t => {
                        tournamentPhases.set(t.id, calculateTournamentPhaseBoundaries(t));
                    });

                    // Apply template assignments to the data structure using O(1) lookups
                    result.assignments.forEach(assignment => {
                        // O(1) lookup instead of findIndex
                        const templateColumnIndex = templateNameToIndex.get(assignment.TemplateName);

                        if (templateColumnIndex === undefined) {
                            return;
                        }

                        // O(1) lookup instead of findIndex with callback
                        const assignmentDate = parseNetDate(assignment.Date);
                        const rowIndex = dateToRowIndex.get(assignmentDate.toDateString());

                        if (rowIndex === undefined) {
                            return;
                        }

                        // Calculate phase using pre-computed boundaries (no Date object creation)
                        let calculatedPhase = null;
                        if (assignment.TournamentId) {
                            const phaseBoundaries = tournamentPhases.get(assignment.TournamentId);
                            if (phaseBoundaries) {
                                const dateTimestamp = normalizeDate(assignmentDate);
                                calculatedPhase = getPhaseForDate(dateTimestamp, phaseBoundaries);
                            }
                        }

                        // Store the template assignment data
                        const cellId = `${templateColumnIndex}_${rowIndex}`;
                        this.data[cellId] = {
                            tournamentId: assignment.TournamentId,
                            tournament: assignment.TournamentId,
                            role: assignment.Role,
                            dailyRate: assignment.DailyRate,
                            dailyRate2: assignment.DailyRate2,
                            // billRate: assignment.BillRate,
                            isWorking: assignment.IsWorking,
                            isTravel: assignment.IsTravel,
                            isAdvanceWeek: assignment.IsAdvanceWeek,
                            notWorking: assignment.NotWorking,
                            travel: assignment.IsTravel,
                            travelTournamentId: assignment.TravelTournamentId, // For travel between tournaments
                            phase: calculatedPhase,
                            assigned: assignment.IsWorking && !assignment.NotWorking && !assignment.IsTravel, // Only true for actual tournament work assignments
                            value: `${assignment.TournamentName || 'Travel/Not Working'} - ${assignment.Role}`
                        };
                    });
                }

                return;
            } catch (error) {
                // Ignore template loading errors
            }
        };

        // Add loadScheduleData method
        instance.loadScheduleData = async function () {
            try {

                const response = await fetch(ProcessUrl('Tournament/GetScheduleAssignments?year=' + this.year));

                if (!response.ok) {
                    throw new Error(`HTTP error! status: ${response.status}`);
                }

                const result = await response.json();

                if (result.success && result.assignments && result.assignments.length > 0) {

                    // Build lookup maps ONCE before processing (O(1) lookups instead of O(n) searches)
                    const staffNameToIndex = new Map();
                    this.columnNames.forEach((name, idx) => staffNameToIndex.set(name, idx));

                    const dateToRowIndex = new Map();
                    this.scheduleRows.forEach((row, idx) => {
                        const rowDate = row.date || row;
                        if (rowDate instanceof Date) {
                            dateToRowIndex.set(rowDate.toDateString(), idx);
                        }
                    });

                    // Pre-calculate phase boundaries for all tournaments ONCE
                    const tournamentPhases = new Map();
                    this.tournaments.forEach(t => {
                        tournamentPhases.set(t.id, calculateTournamentPhaseBoundaries(t));
                    });

                    // Apply assignments to the data structure using O(1) lookups
                    result.assignments.forEach((assignment) => {
                        // O(1) lookup instead of findIndex
                        const staffColumnIndex = staffNameToIndex.get(assignment.StaffName);

                        if (staffColumnIndex === undefined) {
                            return;
                        }

                        // O(1) lookup instead of findIndex with callback
                        const assignmentDate = parseNetDate(assignment.Date);
                        const rowIndex = dateToRowIndex.get(assignmentDate.toDateString());

                        if (rowIndex === undefined) {
                            return;
                        }

                        // Calculate phase using pre-computed boundaries (no Date object creation)
                        let calculatedPhase = null;
                        if (assignment.TournamentId) {
                            const phaseBoundaries = tournamentPhases.get(assignment.TournamentId);
                            if (phaseBoundaries) {
                                const dateTimestamp = normalizeDate(assignmentDate);
                                calculatedPhase = getPhaseForDate(dateTimestamp, phaseBoundaries);
                            }
                        }

                        // Store the assignment data using the expected cell ID format
                        const cellId = `${staffColumnIndex}_${rowIndex}`;
                        const cellData = {
                            tournamentId: assignment.TournamentId,
                            tournament: assignment.TournamentId,
                            role: assignment.Role,
                            dailyRate: assignment.DailyRate,
                            dailyRate2: assignment.DailyRate2,
                            // billRate: assignment.BillRate,
                            isWorking: assignment.IsWorking,
                            isTravel: assignment.IsTravel,
                            isAdvanceWeek: assignment.IsAdvanceWeek,
                            notWorking: assignment.NotWorking,
                            entryType: assignment.EntryType, // Store EntryType (1-5) for absence type differentiation
                            travel: assignment.IsTravel, // Add both formats for consistency
                            travelTournamentId: assignment.TravelTournamentId, // For travel between tournaments
                            phase: calculatedPhase, // Add calculated phase
                            assigned: assignment.IsWorking && !assignment.NotWorking && !assignment.IsTravel, // Only true for actual tournament work assignments
                            clockIn: assignment.ClockIn || "", // Add clock in time
                            clockOut: assignment.ClockOut || "", // Add clock out time
                            hasClockTimes: !!(assignment.ClockIn || assignment.ClockOut), // Flag for visual indicator (either time set)
                            value: `${assignment.TournamentName || 'Travel/Not Working'} - ${assignment.Role}`
                        };
                        this.data[cellId] = cellData;
                    });

                } else {
                    // No saved assignments found
                }

                return;
            } catch (error) {
                // Ignore schedule loading errors
            }
        };

        // Add method to apply loaded assignments to grid visually
        instance.applyLoadedAssignmentsToGrid = function () {
            // With virtual scrolling, we just need to update the render context and re-render visible rows
            // The data is already in this.data, and _renderVisibleRows reads from it

            // Update the render context with latest data
            if (this._renderContext) {
                this._renderContext.dataSource = this.filteredData || this.data;
            }

            // Force re-render of visible rows by resetting the range
            if (this.virtualScrolling) {
                this.virtualScrolling.renderedRange = { start: -1, end: -1 };
            }

            // Re-render visible rows
            if (this._renderVisibleRows) {
                this._renderVisibleRows();
            }
        };

        // Override loadTournamentsFromDatabase on the instance
        instance.loadTournamentsFromDatabase = async function () {
            try {

                // Get tournaments for the current year from API
                const response = await fetch(ProcessUrl('Tournament/GetYearlyTournamentSchedule?year=' + this.year));

                if (!response.ok) {
                    throw new Error(`HTTP error! status: ${response.status}`);
                }

                const data = await response.json();
   
                // Check if this is an error response
                if (data.success === false) {
                    throw new Error(data.message);
                }


                const tournaments = data.Tournaments || [];


                // Transform the backend data to match the frontend structure
                this.tournaments = tournaments.map(t => ({
                    id: t.TournamentId,
                    name: t.TournamentName,
                    location: t.Location,
                    startDate: parseNetDate(t.StartDate),
                    endDate: parseNetDate(t.EndDate),
                    setupStart: parseNetDate(t.SetupStartDate),
                    setupEnd: parseNetDate(t.SetupEndDate),
                    advancedStart: parseNetDate(t.AdvanceWeekStartDate),
                    advancedEnd: parseNetDate(t.AdvanceWeekEndDate),
                    teardownStart: parseNetDate(t.TeardownStartDate),
                    teardownEnd: parseNetDate(t.TeardownEndDate),
                    status: t.Status,
                    clientId: t.ClientId,
                    clientName: t.ClientName,
                    minimumWage: t.MinimumWage || 0,
                    perDiem: t.PerDiem || 0,
                    travelPerDiem: t.TravelPerDiem || 0,
                    billRate: t.BillRate || null,
                    managerBillRate: t.ManagerBillRate || null,
                    printBillRate: t.PrintBillRate || null,
                    otByDay: t.OTByDay || false
                }));

                return this.tournaments;
            } catch (error) {
                
                // Fallback: try to use server-side data if API fails
                if (window.TournamentsData && window.TournamentsData.length > 0) {
                    this.tournaments = window.TournamentsData.map(t => ({
                        id: t.Id,
                        name: t.Name,
                        location: t.Location,
                        startDate: parseNetDate(t.StartDate),
                        endDate: parseNetDate(t.EndDate),
                        setupStart: parseNetDate(t.SetupStartDate),
                        setupEnd: parseNetDate(t.SetupEndDate),
                        advancedStart: parseNetDate(t.AdvanceWeekStartDate),
                        advancedEnd: parseNetDate(t.AdvanceWeekEndDate),
                        teardownStart: parseNetDate(t.TeardownStartDate),
                        teardownEnd: parseNetDate(t.TeardownEndDate),
                        status: t.Status,
                        clientName: t.ClientName,
                        minimumWage: t.MinimumWage || 0,
                        perDiem: t.PerDiem || 0,
                        travelPerDiem: t.TravelPerDiem || 0,
                        billRate: t.BillRate || null,
                        managerBillRate: t.ManagerBillRate || null,
                        printBillRate: t.PrintBillRate || null,
                        otByDay: t.OTByDay || false
                    }));
                } else {
                    this.tournaments = [];
                }
                return this.tournaments;
            }
        };

        // Add method to save all changes to server
        instance.saveScheduleToServer = async function () {
            try {
                // Show loading indicator to prevent UI freeze perception
                if (window.showLoading) {
                    window.showLoading();
                }


                const assignments = [];

                // Only process tracked changes instead of all grid data
                for (const [cellKey, change] of pendingChanges) {
                    const { rowIndex, colIndex, cellData } = change;
                    const scheduleRow = this.scheduleRows[rowIndex];
                    const staffName = this.columnNames[colIndex];

                    // Handle deletions (cellData is null)
                    if (cellData === null) {
                        // For deletions, we could send a deletion request to the server
                        // For now, we'll skip deletions in the assignments array
                        // since the server logic clears existing assignments anyway
                        continue;
                    }

                    if (cellData && (cellData.tournamentId || cellData.travel || cellData.isTravel || cellData.notWorking)) {
                        // Find staff member's user ID
                        const staffMember = window.EventStaffData?.find(s => s.FullName === staffName);
                        if (staffMember) {
                            // Get default role and rate from tournament roles
                            const defaultRole = window.TournamentRoles && window.TournamentRoles.length > 0
                                ? window.TournamentRoles.find(role => role.Title === 'Unassigned') || window.TournamentRoles[0]
                                : { Title: 'Unassigned', DailyRate: 350.00 };

                            // Validate and clean data before sending
                            const role = cellData.role || defaultRole.Title || 'Unassigned';

                            // Find the daily rate for the specific role being assigned
                            const roleData = window.TournamentRoles && window.TournamentRoles.length > 0
                                ? window.TournamentRoles.find(r => r.Title === role)
                                : null;
                            const dailyRate = cellData.dailyRate || (roleData ? roleData.DailyRate : null) || defaultRole.DailyRate || 350.00;
                            
                            // Extract date from schedule row object
                            let formattedDate;
                            let dateToFormat = null;
                            
                            if (scheduleRow && scheduleRow.date instanceof Date) {
                                // Schedule row has date property
                                dateToFormat = scheduleRow.date;
                            } else if (scheduleRow instanceof Date) {
                                // Schedule row is a date itself
                                dateToFormat = scheduleRow;
                            } else if (typeof scheduleRow === 'string') {
                                // Schedule row is a string date
                                dateToFormat = new Date(scheduleRow);
                            } else {
                                continue; // Skip this assignment
                            }
                            
                            // Format the date
                            if (dateToFormat instanceof Date && !isNaN(dateToFormat.getTime())) {
                                formattedDate = dateToFormat.toISOString().split('T')[0];
                            } else {
                                continue; // Skip this assignment
                            }
                            // Determine EntryType: 1=Tournament, 2=Travel, 3=Sick, 4=TimeOffOffsite, 5=TimeOffOnsite
                            let entryType = 1;  // Default: Tournament
                            if (cellData.isTravel || cellData.travel) {
                                entryType = 2;  // Travel
                            } else if (cellData.notWorking) {
                                // For "not working" entries, use the specific entryType stored in cellData
                                // 3 = Sick, 4 = Time Off Offsite, 5 = Time Off Onsite
                                entryType = cellData.entryType || 3;  // Default to Sick if not specified
                            }

                            assignments.push({
                                UserId: staffMember.UserId,
                                TournamentId: cellData.tournamentId || null,
                                Date: formattedDate,
                                Role: role,
                                DailyRate: parseFloat(dailyRate),
                                DailyRate2: cellData.dailyRate2, // Add DailyRate2 from popup
                                // BillRate: cellData.billRate,
                                IsTravel: cellData.isTravel || cellData.travel || false,
                                IsAdvanceWeek: cellData.isAdvanceWeek || false,
                                IsWorking: !cellData.notWorking && !(cellData.travel || cellData.isTravel), // Working unless not working or travel
                                NotWorking: cellData.notWorking || false,
                                EntryType: entryType,  // Send the specific EntryType
                                TravelTournamentId: cellData.travelTournamentId || null // For travel between tournaments
                            });
                        }
                    }
                }



                if (assignments.length > 0) {
                    const payload = JSON.stringify(assignments);

                    const response = await fetch(ProcessUrl('Tournament/SaveSchedule'), {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                        },
                        body: payload
                    });
                    

                    if (!response.ok) {
                        const errorText = await response.text();
                        throw new Error(`HTTP error! status: ${response.status}`);
                    }

                    const responseText = await response.text();
                    
                    let result;
                    try {
                        result = JSON.parse(responseText);
                    } catch (parseError) {
                        throw new Error('Server returned invalid JSON response');
                    }

                    if (result.success) {
                        // Clear tracked changes after successful save
                        this.clearTrackedChanges();
                        if (window.showSuccess) {
                            window.showSuccess(result.message || 'Schedule saved successfully');
                        }
                    } else {
                        throw new Error(result.message || 'Failed to save schedule');
                    }
                } else {
                    // No assignments to save
                }
            } catch (error) {
                if (window.showError) {
                    window.showError('Failed to save schedule: ' + error.message);
                }
            } finally {
                // Always hide loading indicator
                if (window.hideLoading) {
                    window.hideLoading();
                }
            }
        };

        // Add method to save template assignments to server
        instance.saveTemplateScheduleToServer = async function () {
            try {
                
                const templateAssignments = [];
                
                // Process template columns only
                for (const [cellKey, cellData] of Object.entries(this.data)) {
                    const [colIndex, rowIndex] = cellKey.split('_').map(Number);
                    const templateName = this.columnNames[colIndex];
                    
                    // Only process template columns
                    if (!templateName || !templateName.startsWith('Template')) {
                        continue;
                    }
                    
                    const scheduleRow = this.scheduleRows[rowIndex];
                    
                    if (cellData && (cellData.tournamentId || cellData.travel || cellData.isTravel || cellData.notWorking)) {
                        // Format the date
                        let formattedDate;
                        let dateToFormat = null;
                        
                        if (scheduleRow && scheduleRow.date instanceof Date) {
                            dateToFormat = scheduleRow.date;
                        } else if (scheduleRow instanceof Date) {
                            dateToFormat = scheduleRow;
                        } else if (typeof scheduleRow === 'string') {
                            dateToFormat = new Date(scheduleRow);
                        } else {
                            continue;
                        }
                        
                        if (dateToFormat instanceof Date && !isNaN(dateToFormat.getTime())) {
                            formattedDate = dateToFormat.toISOString().split('T')[0];
                        } else {
                            continue;
                        }
                        
                        // Get default role and rate
                        const defaultRole = window.TournamentRoles && window.TournamentRoles.length > 0
                            ? window.TournamentRoles.find(role => role.Title === 'Unassigned') || window.TournamentRoles[0]
                            : { Title: 'Unassigned', DailyRate: 350.00 };

                        const role = cellData.role || defaultRole.Title || 'Unassigned';

                        // Find the daily rate for the specific role being assigned
                        const roleData = window.TournamentRoles && window.TournamentRoles.length > 0
                            ? window.TournamentRoles.find(r => r.Title === role)
                            : null;
                        const dailyRate = cellData.dailyRate || (roleData ? roleData.DailyRate : null) || defaultRole.DailyRate || 350.00;
                        
                        templateAssignments.push({
                            TemplateName: templateName,
                            Date: formattedDate,
                            TournamentId: cellData.tournamentId || null,
                            Role: role,
                            DailyRate: parseFloat(dailyRate),
                            DailyRate2: cellData.dailyRate2,
                            // BillRate: cellData.billRate,
                            IsTravel: cellData.isTravel || cellData.travel || false,
                            IsAdvanceWeek: cellData.isAdvanceWeek || false,
                            IsWorking: !cellData.notWorking && !(cellData.travel || cellData.isTravel),
                            NotWorking: cellData.notWorking || false,
                            TravelTournamentId: cellData.travelTournamentId || null // For travel between tournaments
                        });
                    }
                }
                
                
                if (templateAssignments.length > 0) {
                    const response = await fetch(ProcessUrl('Tournament/SaveTemplateSchedule'), {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/json',
                        },
                        body: JSON.stringify(templateAssignments)
                    });
                    
                    if (!response.ok) {
                        throw new Error(`HTTP error! status: ${response.status}`);
                    }
                    
                    const result = await response.json();
                    
                    if (result.success) {
                        if (window.showSuccess) {
                            window.showSuccess(result.message || 'Template schedules saved successfully');
                        }
                    } else {
                        throw new Error(result.message || 'Failed to save template schedules');
                    }
                } else {
                }
            } catch (error) {
                if (window.showError) {
                    window.showError('Failed to save template schedules: ' + error.message);
                }
            }
        };

        // Override backend methods on the instance to ensure they're accessible
        instance.saveTournamentToBackend = Spreadsheet.prototype.saveTournamentToBackend;
        instance.updateTournamentInBackend = Spreadsheet.prototype.updateTournamentInBackend;
        instance.deleteTournamentFromBackend = Spreadsheet.prototype.deleteTournamentFromBackend;

        return true;
    }

    async function initializeWithServerData(instance) {
        try {
            // Clear any existing localStorage data
            const keysToRemove = [];
            for (let i = 0; i < localStorage.length; i++) {
                const key = localStorage.key(i);
                if (key && key.includes('tournament-schedule-data')) {
                    keysToRemove.push(key);
                }
            }

            if (keysToRemove.length > 0) {
                keysToRemove.forEach(key => localStorage.removeItem(key));
            }

            // Load data from server in correct order
            await instance.loadColumnsFromDatabase();
            await instance.loadTournamentsFromDatabase();
            instance.generateScheduleRows();
            await instance.loadScheduleData();
            instance.createGrid();
            instance.applyLoadedAssignmentsToGrid();
            await instance.loadTemplateData();
            instance.applyLoadedAssignmentsToGrid();

            // Sort columns by assignment count after data is loaded
            instance.sortColumnsByAssignmentCount();
        } catch (error) {
            console.error('Init error:', error);
        }
    }

})();