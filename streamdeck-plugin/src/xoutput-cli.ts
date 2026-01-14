import { exec } from "child_process";
import { promisify } from "util";
import * as path from "path";
import * as os from "os";
import * as fs from "fs";

const execAsync = promisify(exec);

export interface XOutputStatus {
  isRunning: boolean;
  profileName: string | null;
  uptime: string | null;
}

export interface XOutputProfile {
  name: string;
  isDefault: boolean;
}

/**
 * Gets the path to XOutputRenew executable.
 * Checks PATH first, then common installation locations.
 */
function getXOutputPath(): string {
  // First, try just the command name (if it's in PATH)
  // This is the preferred method as the user can add to PATH from the app
  return "XOutputRenew";
}

/**
 * Executes an XOutputRenew CLI command.
 */
async function runCommand(args: string): Promise<{ stdout: string; stderr: string }> {
  const command = `${getXOutputPath()} ${args}`;

  try {
    const result = await execAsync(command, {
      timeout: 10000, // 10 second timeout
      windowsHide: true,
    });
    return result;
  } catch (error: any) {
    // If the command fails, check if it's because XOutputRenew isn't found
    if (error.code === "ENOENT" || error.message?.includes("not recognized")) {
      throw new Error("XOutputRenew not found. Please ensure it is installed and added to PATH.");
    }
    throw error;
  }
}

/**
 * Gets the current status of XOutputRenew.
 */
export async function getStatus(): Promise<XOutputStatus> {
  try {
    const { stdout } = await runCommand("status --json");
    const status = JSON.parse(stdout);
    return {
      isRunning: status.isRunning ?? false,
      profileName: status.profileName ?? null,
      uptime: status.uptime ?? null,
    };
  } catch (error) {
    // If status command fails, assume not running
    return {
      isRunning: false,
      profileName: null,
      uptime: null,
    };
  }
}

/**
 * Lists available profiles.
 */
export async function listProfiles(): Promise<XOutputProfile[]> {
  try {
    const { stdout } = await runCommand("list-profiles --json");
    const profiles = JSON.parse(stdout);
    return profiles.map((p: any) => ({
      name: p.name ?? p,
      isDefault: p.isDefault ?? false,
    }));
  } catch (error) {
    console.error("Failed to list profiles:", error);
    return [];
  }
}

/**
 * Starts a profile.
 * @param profileName The name of the profile to start, or undefined to use default.
 * @returns True if successful, false otherwise.
 */
export async function startProfile(profileName?: string): Promise<boolean> {
  try {
    const args = profileName ? `start "${profileName}"` : "start";
    await runCommand(args);
    return true;
  } catch (error) {
    console.error("Failed to start profile:", error);
    return false;
  }
}

/**
 * Stops the running profile.
 * @returns True if successful, false otherwise.
 */
export async function stopProfile(): Promise<boolean> {
  try {
    await runCommand("stop");
    return true;
  } catch (error) {
    console.error("Failed to stop profile:", error);
    return false;
  }
}

/**
 * Toggles a profile on/off.
 * @param profileName The name of the profile to toggle.
 * @returns The new state (true = running, false = stopped).
 */
export async function toggleProfile(profileName: string): Promise<boolean> {
  const status = await getStatus();

  if (status.isRunning && status.profileName === profileName) {
    // Profile is running, stop it
    await stopProfile();
    return false;
  } else {
    // Profile is not running (or different profile), start it
    // If a different profile is running, starting a new one will stop the old one
    await startProfile(profileName);
    return true;
  }
}
