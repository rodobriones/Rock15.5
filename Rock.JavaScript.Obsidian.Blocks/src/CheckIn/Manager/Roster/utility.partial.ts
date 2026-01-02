import { CheckInStatus } from "@Obsidian/Enums/Event/checkInStatus";
import { RockDateTime } from "@Obsidian/Utility/rockDateTime";
import { RosterAttendanceRecord, RosterSingleAttendanceRecord, RosterViewMode } from "./types.partial";
import { RosterAttendanceBag } from "@Obsidian/ViewModels/Blocks/CheckIn/Manager/Roster/rosterAttendanceBag";

/**
 * Calculates the duration in minutes since the check-in time.
 *
 * @param checkInTime The time the check-in happened as an ISO string.
 *
 * @returns The number of minutes since the check-in happened.
 */
export function calculateDuration(checkInTime: string): number {
    const date = RockDateTime.parseISO(checkInTime);

    if (!date) {
        return 0;
    }

    const localCheckInTime = date.localDateTime;
    const now = RockDateTime.now();
    const durationInSeconds = (now.toMilliseconds() - localCheckInTime.toMilliseconds()) / 1000;

    return Math.max(0, Math.floor(durationInSeconds / 60));
}

/**
 * Determines if the given status matches the selected roster view mode.
 *
 * @param status The status for the attendance record.
 * @param mode The roster view mode displayed in the UI.
 *
 * @returns true if the status matches the selected roster mode.
 */
export function statusMatchesMode(status: CheckInStatus, mode: RosterViewMode): boolean {
    switch (mode) {
        case RosterViewMode.CheckedIn:
            return status === CheckInStatus.NotPresent;
        case RosterViewMode.Present:
            return status === CheckInStatus.Present;
        case RosterViewMode.CheckedOut:
            return status === CheckInStatus.CheckedOut;
        default:
            return true;
    }
}

/**
 * Updates a single attendance record in the grid with new data.
 *
 * @param attendance The attendance record that contains the updated data.
 * @param record The record in the grid to update.
 */
export function updateSingleGridAttendanceRecord(attendance: RosterAttendanceBag, record: RosterSingleAttendanceRecord): void {
    record.attendee = attendance.attendee!;
    record.checkInTime = attendance.checkInTime!;
    record.code = attendance.code!;
    record.schedule = attendance.schedule!;
    record.group = attendance.group!;
    record.area = attendance.area!;
    record.status = attendance.status;
    record.isCheckoutSupported = attendance.isCheckoutSupported;
    record.isPresenceSupported = attendance.isPresenceSupported;

    record.checkInDuration = calculateDuration(record.checkInTime);
}

/**
 * Retrieves single attendance records from a roster attendance record. If the
 * attendance record is compound, all single records are returned; otherwise,
 * the original record is returned in an array of one.
 *
 * @param record The roster attendance record.
 *
 * @returns An array of single attendance records.
 */
export function getSingleAttendanceRecords(record: RosterAttendanceRecord): RosterSingleAttendanceRecord[] {
    if ("records" in record) {
        return record.records;
    }
    else {
        return [record];
    }
}

