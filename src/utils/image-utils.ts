import {IMAGE_SIZE_SINGLE_CELL, IMAGE_SIZE_FULL, PLACEHOLDER_COLOR} from './constants';
import type {MediaInfo} from './media-manager';
import {playIconSvg, pauseIconSvg} from './icons';

function createSvg(width: number, height: number, content: string): string {
    return `<svg width="${width}" height="${height}" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink">
${content}
</svg>`;
}

export function generatePlaceholderImage(size: number = IMAGE_SIZE_SINGLE_CELL): string {
    const content = `		<rect width="${size}" height="${size}" fill="${PLACEHOLDER_COLOR}"/>`;
    const svg = createSvg(size, size, content);
    const base64 = Buffer.from(svg).toString('base64');
    return `data:image/svg+xml;base64,${base64}`;
}

function createAppIconOverlay(iconBase64: string, x: number, y: number, size: number): string {
    const radius = size / 2;
    return `
		<g>
			<circle cx="${x + radius}" cy="${y + radius}" r="${radius + 2}" fill="rgba(0,0,0,0.6)"/>
			<image href="data:image/png;base64,${iconBase64}" 
				x="${x}" y="${y}" 
				width="${size}" height="${size}" 
				preserveAspectRatio="xMidYMid meet"/>
		</g>`;
}

function createStatusOverlay(status: 'Playing' | 'Paused' | 'Stopped', x: number, y: number, size: number): string {
    const radius = size / 2;
    const centerX = x + radius;
    const centerY = y + radius;

    const iconSvg = status === 'Playing' ? playIconSvg : pauseIconSvg;
    const svgContent = iconSvg.replace(/^<svg[^>]*>|<\/svg>$/g, '');
    const scale = size / 32;

    return `
		<g>
			<circle cx="${centerX}" cy="${centerY}" r="${radius + 2}" fill="rgba(0,0,0,0.6)"/>
			<g transform="translate(${x}, ${y}) scale(${scale})">
				${svgContent}
			</g>
		</g>`;
}

function createSvgWithOverlay(baseImageDataUrl: string, overlayElements: string, imageSize: number): string {
    if (baseImageDataUrl.startsWith('data:image/svg+xml;base64,')) {
        const svgMatch = baseImageDataUrl.match(/data:image\/svg\+xml;base64,(.+)/);
        if (svgMatch) {
            const decodedSvg = Buffer.from(svgMatch[1], 'base64').toString('utf-8');
            const svgWithoutTags = decodedSvg.replace(/^<svg[^>]*>|<\/svg>$/g, '').trim();
            const content = `${svgWithoutTags}
		${overlayElements}`;
            return createSvg(imageSize, imageSize, content);
        }
    }

    let baseImageBase64 = baseImageDataUrl;
    if (baseImageDataUrl.startsWith('data:image/png;base64,')) {
        baseImageBase64 = baseImageDataUrl.substring('data:image/png;base64,'.length);
    }

    const content = `<image href="data:image/png;base64,${baseImageBase64}" width="${imageSize}" height="${imageSize}"/>
		${overlayElements}`;
    return createSvg(imageSize, imageSize, content);
}

export async function applyOverlayToImage(
    baseImageDataUrl: string,
    overlayMode: 'none' | 'icon' | 'status' | 'both',
    mediaInfo: MediaInfo,
    imageSize: number
): Promise<string> {
    if (overlayMode === 'none') {
        return baseImageDataUrl;
    }

    const showIcon = (overlayMode === 'icon' || overlayMode === 'both') && mediaInfo.AppIconBase64;
    const showStatus = (overlayMode === 'status' || overlayMode === 'both') && mediaInfo.Status;

    if (!showIcon && !showStatus) {
        return baseImageDataUrl;
    }

    const padding = Math.floor(imageSize * 0.05);
    const iconSize = Math.floor(imageSize * 0.25);
    const statusSize = iconSize;

    let overlayElements = '';

    if (showIcon && mediaInfo.AppIconBase64) {
        const iconX = padding;
        const iconY = padding;
        overlayElements += createAppIconOverlay(mediaInfo.AppIconBase64, iconX, iconY, iconSize);
    }

    if (showStatus && mediaInfo.Status) {
        const statusX = imageSize - padding - statusSize;
        const statusY = padding;
        overlayElements += createStatusOverlay(mediaInfo.Status, statusX, statusY, statusSize);
    }

    const svg = createSvgWithOverlay(baseImageDataUrl, overlayElements, imageSize);
    const base64 = Buffer.from(svg).toString('base64');
    return `data:image/svg+xml;base64,${base64}`;
}