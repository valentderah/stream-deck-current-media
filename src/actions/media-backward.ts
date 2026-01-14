import { action, KeyDownEvent, SingletonAction } from '@elgato/streamdeck';
import { seekBackward } from '../utils/media-manager';

@action({ UUID: 'ru.valentderah.current-media.media-backward' })
export class MediaBackwardAction extends SingletonAction {
	override onKeyDown(ev: KeyDownEvent): void {
		seekBackward();
	}
}
