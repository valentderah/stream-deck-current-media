import streamDeck from "@elgato/streamdeck";

import { IncrementCounter } from "./actions/increment-counter";
import { MediaInfoAction } from "./actions/media-info";

// We can enable "trace" logging so that all messages between the Stream Deck, and the plugin are recorded. When storing sensitive information
streamDeck.logger.setLevel("trace");

// Register the increment action.
streamDeck.actions.registerAction(new IncrementCounter());

// Register the media info action.
streamDeck.actions.registerAction(new MediaInfoAction());

// Finally, connect to the Stream Deck.
streamDeck.connect();
