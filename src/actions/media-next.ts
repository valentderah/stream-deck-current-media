import { action, KeyDownEvent, SingletonAction } from '@elgato/streamdeck';
import { nextMedia } from '../utils/media-manager';

@action({ UUID: 'ru.valentderah.current-media.media-next' })
export class MediaNextAction extends SingletonAction {
	override onKeyDown(ev: KeyDownEvent): void {
		nextMedia();
	}
}
