import streamDeck, { LogLevel } from "@elgato/streamdeck";

import { StartProfileAction } from "./actions/start-profile";
import { StopProfileAction } from "./actions/stop-profile";
import { ToggleProfileAction } from "./actions/toggle-profile";

// Set up logging
streamDeck.logger.setLevel(LogLevel.DEBUG);

// Register actions
streamDeck.actions.registerAction(new StartProfileAction());
streamDeck.actions.registerAction(new StopProfileAction());
streamDeck.actions.registerAction(new ToggleProfileAction());

// Connect to Stream Deck
streamDeck.connect();
