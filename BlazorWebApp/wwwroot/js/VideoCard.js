// Video card play/pause control for hover functionality
export function playVideo(videoId) {
    const video = document.getElementById(videoId);
    if (video) {
        video.play().catch(() => { });
    }
}

export function pauseVideo(videoId) {
    const video = document.getElementById(videoId);
    if (video) {
        video.pause();
        // Reset to first frame for consistent thumbnail
        video.currentTime = 0;
    }
}
