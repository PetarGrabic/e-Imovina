// Thin camera-scan wrapper around the vendored jsQR library (wwwroot/lib/jsqr/jsQR.min.js).
// Loaded globally (see App.razor) so it's available to any page that calls window.qrScanner.*.
window.qrScanner = (function () {
    let stream = null;
    let animationFrameId = null;
    let canvas = null;
    let context = null;

    async function start(videoElementId, dotNetRef) {
        const video = document.getElementById(videoElementId);
        if (!video) {
            return;
        }

        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: "environment" } });
        video.srcObject = stream;
        await video.play();

        canvas = document.createElement("canvas");
        context = canvas.getContext("2d", { willReadFrequently: true });

        const tick = () => {
            if (video.readyState === video.HAVE_ENOUGH_DATA) {
                canvas.width = video.videoWidth;
                canvas.height = video.videoHeight;
                context.drawImage(video, 0, 0, canvas.width, canvas.height);
                const imageData = context.getImageData(0, 0, canvas.width, canvas.height);
                const code = jsQR(imageData.data, imageData.width, imageData.height);
                if (code && code.data) {
                    dotNetRef.invokeMethodAsync("OnCodeScanned", code.data);
                    return;
                }
            }
            animationFrameId = requestAnimationFrame(tick);
        };
        animationFrameId = requestAnimationFrame(tick);
    }

    function stop() {
        if (animationFrameId !== null) {
            cancelAnimationFrame(animationFrameId);
            animationFrameId = null;
        }
        if (stream) {
            stream.getTracks().forEach(track => track.stop());
            stream = null;
        }
    }

    return { start, stop };
})();
