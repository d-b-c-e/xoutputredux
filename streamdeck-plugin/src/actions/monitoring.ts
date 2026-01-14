import {
  action,
  KeyDownEvent,
  SingletonAction,
  WillAppearEvent,
} from "@elgato/streamdeck";

import { startMonitoring, stopMonitoring, toggleMonitoring, getStatus } from "../xoutput-cli";

/**
 * Action to start game monitoring.
 */
@action({ UUID: "com.xoutputrenew.start-monitoring" })
export class StartMonitoringAction extends SingletonAction {
  override async onWillAppear(ev: WillAppearEvent): Promise<void> {
    await ev.action.setTitle("Start\nMonitor");
  }

  override async onKeyDown(ev: KeyDownEvent): Promise<void> {
    try {
      const success = await startMonitoring();
      if (success) {
        await ev.action.showOk();
      } else {
        await ev.action.showAlert();
      }
    } catch (error) {
      console.error("Failed to start monitoring:", error);
      await ev.action.showAlert();
    }
  }
}

/**
 * Action to stop game monitoring.
 */
@action({ UUID: "com.xoutputrenew.stop-monitoring" })
export class StopMonitoringAction extends SingletonAction {
  override async onWillAppear(ev: WillAppearEvent): Promise<void> {
    await ev.action.setTitle("Stop\nMonitor");
  }

  override async onKeyDown(ev: KeyDownEvent): Promise<void> {
    try {
      const success = await stopMonitoring();
      if (success) {
        await ev.action.showOk();
      } else {
        await ev.action.showAlert();
      }
    } catch (error) {
      console.error("Failed to stop monitoring:", error);
      await ev.action.showAlert();
    }
  }
}

/**
 * Action to toggle game monitoring on/off.
 */
@action({ UUID: "com.xoutputrenew.toggle-monitoring" })
export class ToggleMonitoringAction extends SingletonAction {
  private statusCheckInterval: NodeJS.Timeout | null = null;

  override async onWillAppear(ev: WillAppearEvent): Promise<void> {
    await this.updateState(ev.action);

    // Start periodic status check
    if (!this.statusCheckInterval) {
      this.statusCheckInterval = setInterval(async () => {
        await this.updateState(ev.action);
      }, 5000);
    }
  }

  override async onWillDisappear(): Promise<void> {
    if (this.statusCheckInterval) {
      clearInterval(this.statusCheckInterval);
      this.statusCheckInterval = null;
    }
  }

  override async onKeyDown(ev: KeyDownEvent): Promise<void> {
    try {
      const isMonitoring = await toggleMonitoring();
      await ev.action.setState(isMonitoring ? 1 : 0);
      await ev.action.showOk();
    } catch (error) {
      console.error("Failed to toggle monitoring:", error);
      await ev.action.showAlert();
    }
  }

  private async updateState(action: any): Promise<void> {
    try {
      const status = await getStatus();
      await action.setState(status.isMonitoring ? 1 : 0);
    } catch (error) {
      await action.setState(0);
    }
  }
}
