// BlinkVision — the Mac camera helper for Karoshi's blink mechanic.
//
// Reads the webcam, finds your eyes with Apple's Vision framework (built into macOS — no
// downloads, no Python), works out how closed they are, and sends one small JSON packet
// per frame to the game on 127.0.0.1 (UDP port 5066). Frames are processed in memory and
// thrown away: nothing is saved, nothing is sent anywhere else.
//
// Build:  ./build.sh            (produces build/BlinkVision)
// Run:    build/BlinkVision      (the game also starts it for you after you consent with F8)
//
// Packet — the same one tools/blink/blink_server.py sends, read by UdpBlinkSource.cs:
//   {"seq":1234,"closed":0.93,"conf":0.88,"src":"vision","capture":…,"sent":…,"fps":30.0,
//    "ratio":0.21,"open":0.33}    (ratio/open: this frame's eye height and your usual one)

import AVFoundation
import CoreMedia
import Foundation
import Network
import Vision

// ---- options -------------------------------------------------------------------------------

struct Options {
    var port: UInt16 = 5066
    var fps: Double = 60
    var quiet = false
    var cameraIndex = 0
    var listCameras = false
    var listFormats = false
}

func parseOptions() -> Options {
    var o = Options()
    var args = CommandLine.arguments.dropFirst().makeIterator()
    while let a = args.next() {
        switch a {
        case "--port": if let v = args.next(), let p = UInt16(v) { o.port = p }
        case "--fps": if let v = args.next(), let f = Double(v) { o.fps = f }
        case "--camera": if let v = args.next(), let i = Int(v) { o.cameraIndex = i }
        case "--list-cameras": o.listCameras = true
        case "--list-formats": o.listFormats = true
        case "--quiet": o.quiet = true
        case "-h", "--help":
            print("""
            BlinkVision — webcam blink tracking for Karoshi (macOS, Apple Vision).
              --port N         UDP port the game listens on (default 5066)
              --fps N          camera frame rate to ask for (default 60)
              --camera N       which camera (see --list-cameras; default 0)
              --list-cameras   print the cameras and exit
              --list-formats   print each camera's sizes and frame rates and exit (doesn't turn it on)
              --quiet          no live status line
            Nothing is recorded; one number per frame goes to 127.0.0.1 only.
            """)
            exit(0)
        default: break
        }
    }
    return o
}

func log(_ s: String) {
    FileHandle.standardOutput.write((s + "\n").data(using: .utf8)!)
}

// ---- UDP to the game ---------------------------------------------------------------------------

final class Sender {
    private let connection: NWConnection
    private var seq = 0
    private(set) var fps: Double = 0
    private var last: Double = 0

    init(port: UInt16) {
        connection = NWConnection(host: "127.0.0.1", port: NWEndpoint.Port(rawValue: port)!, using: .udp)
        connection.start(queue: DispatchQueue(label: "blink.udp"))
    }

    func send(closed: Double, conf: Double, capture: Double, ratio: Double, open: Double) {
        let now = Date().timeIntervalSince1970
        if last > 0 { let dt = now - last; if dt > 0 { fps = fps == 0 ? 1 / dt : fps * 0.9 + 0.1 / dt } }
        last = now
        seq += 1
        let c = max(0, min(1, closed)), k = max(0, min(1, conf))
        let json = String(format: "{\"seq\":%d,\"closed\":%.4f,\"conf\":%.3f,\"src\":\"vision\",\"capture\":%.4f,\"sent\":%.4f,\"fps\":%.1f,\"ratio\":%.4f,\"open\":%.4f}",
                          seq, c, k, capture, Date().timeIntervalSince1970, fps, ratio, open)
        connection.send(content: json.data(using: .utf8), completion: .idempotent)
    }
}

// ---- how shut the eyes are ------------------------------------------------------------------------

// Eye openness: how tall the eye's outline is across its own axis (corner to corner),
// divided by its length, so tilting your head doesn't change it. Measured in image pixels,
// since Vision's per-face coordinates stretch x and y by different amounts.
func openness(_ region: VNFaceLandmarkRegion2D?, imageSize: CGSize) -> Double? {
    guard let r = region, r.pointCount >= 6 else { return nil }
    let pts = r.pointsInImage(imageSize: imageSize)
    var a = pts[0], b = pts[1], longest = -1.0
    for i in 0..<pts.count {
        for j in (i + 1)..<pts.count {
            let dx = Double(pts[j].x - pts[i].x), dy = Double(pts[j].y - pts[i].y)
            if dx * dx + dy * dy > longest { longest = dx * dx + dy * dy; a = pts[i]; b = pts[j] }
        }
    }
    let length = longest.squareRoot()
    guard length > 1 else { return nil }
    let ux = Double(b.x - a.x) / length, uy = Double(b.y - a.y) / length
    var lo = Double.greatestFiniteMagnitude, hi = -Double.greatestFiniteMagnitude
    for p in pts {
        let h = Double(p.y - a.y) * ux - Double(p.x - a.x) * uy
        lo = min(lo, h); hi = max(hi, h)
    }
    return (hi - lo) / length
}

// The "open" reference is how open your eyes usually are (the 80th percentile of the last
// eight seconds), so it adapts to your face, the camera angle and the light by itself.
// Closed is reported against it: 0 = as open as usual, 1 = at or below `shutFraction` of
// that. Vision's eye outline rarely collapses completely on a blink, so shut is taken as
// a bit over half the usual height; the game's calibration (F9) then fits your own range.
final class EyeScale {
    private var history: [Double] = []
    private let keep: Int
    let shutFraction = 0.55
    private(set) var open = 0.0

    init(fps: Double) { keep = max(90, Int(fps * 8)) }

    func closed(_ ratio: Double) -> Double {
        history.append(ratio)
        if history.count > keep { history.removeFirst(history.count - keep) }
        let sorted = history.sorted()
        open = sorted[Int(Double(sorted.count - 1) * 0.8)]
        guard open > 1e-4 else { return 0 }
        return max(0, min(1, (1 - ratio / open) / (1 - shutFraction)))
    }
}

// The fastest frame rate the camera offers (up to `wantFps`), then the size nearest
// 1280x720: plenty of detail around the eyes, and quick for Vision to read.
func bestFormat(_ device: AVCaptureDevice, wantFps: Double) -> (format: AVCaptureDevice.Format, range: AVFrameRateRange)? {
    var best: (format: AVCaptureDevice.Format, range: AVFrameRateRange, score: Double)?
    for f in device.formats {
        let dims = CMVideoFormatDescriptionGetDimensions(f.formatDescription)
        guard dims.height >= 480 else { continue }
        for range in f.videoSupportedFrameRateRanges {
            let score = min(range.maxFrameRate, wantFps) * 10_000 - Double(abs(Int(dims.height) - 720))
            if best == nil || score > best!.score { best = (f, range, score) }
        }
    }
    return best.map { ($0.format, $0.range) }
}

// ---- the camera -----------------------------------------------------------------------------------

final class Tracker: NSObject, AVCaptureVideoDataOutputSampleBufferDelegate {
    let session = AVCaptureSession()
    let sender: Sender
    var scale = EyeScale(fps: 30)
    let quiet: Bool
    let wantFps: Double
    let queue = DispatchQueue(label: "blink.camera")
    let request: VNDetectFaceLandmarksRequest = {
        let r = VNDetectFaceLandmarksRequest()
        r.revision = VNDetectFaceLandmarksRequestRevision3
        r.constellation = .constellation76Points   // more points along each eyelid
        return r
    }()
    var lastStatus = 0.0
    var frames = 0
    var faceSeen = false
    var size = ""

    init(options: Options) {
        sender = Sender(port: options.port)
        quiet = options.quiet
        wantFps = options.fps
    }

    func start(device: AVCaptureDevice) throws {
        let chosen = bestFormat(device, wantFps: wantFps)
        scale = EyeScale(fps: chosen.map { min($0.range.maxFrameRate, wantFps) } ?? 30)

        session.beginConfiguration()
        let input = try AVCaptureDeviceInput(device: device)
        guard session.canAddInput(input) else { throw NSError(domain: "BlinkVision", code: 1, userInfo: [NSLocalizedDescriptionKey: "can't use this camera"]) }
        session.addInput(input)
        let output = AVCaptureVideoDataOutput()
        output.alwaysDiscardsLateVideoFrames = true
        output.setSampleBufferDelegate(self, queue: queue)
        guard session.canAddOutput(output) else { throw NSError(domain: "BlinkVision", code: 2, userInfo: [NSLocalizedDescriptionKey: "can't read frames"]) }
        session.addOutput(output)
        session.commitConfiguration()
        session.startRunning()

        // After the session has started, so its preset can't put the format back.
        if let (format, range) = chosen {
            do {
                try device.lockForConfiguration()
                device.activeFormat = format
                var frame = range.minFrameDuration   // the fastest this range allows
                if range.maxFrameRate > wantFps {
                    let wanted = CMTime(value: 1, timescale: CMTimeScale(wantFps))
                    if CMTimeCompare(wanted, range.minFrameDuration) >= 0 && CMTimeCompare(wanted, range.maxFrameDuration) <= 0 { frame = wanted }
                }
                device.activeVideoMinFrameDuration = frame
                device.activeVideoMaxFrameDuration = frame
                device.unlockForConfiguration()
            } catch {
                log("BlinkVision: couldn't change the camera's format (\(error.localizedDescription)); using its default.")
            }
        }
        let dims = CMVideoFormatDescriptionGetDimensions(device.activeFormat.formatDescription)
        let top = device.activeFormat.videoSupportedFrameRateRanges.map { $0.maxFrameRate }.max() ?? 0
        size = "\(dims.width)x\(dims.height)"
        log("BlinkVision: camera '\(device.localizedName)' at \(size), up to \(Int(top.rounded())) frames a second. Sending to 127.0.0.1 only; frames are not saved.")
        if top < 50 {
            log("BlinkVision: \(Int(top.rounded())) fps is this camera's limit (normal for a built-in Mac camera). A blink still spans 3 to 10 frames.")
        }
    }

    func captureOutput(_ output: AVCaptureOutput, didOutput sampleBuffer: CMSampleBuffer, from connection: AVCaptureConnection) {
        let capture = Date().timeIntervalSince1970
        guard let pixels = CMSampleBufferGetImageBuffer(sampleBuffer) else { return }
        let image = CGSize(width: CVPixelBufferGetWidth(pixels), height: CVPixelBufferGetHeight(pixels))
        let handler = VNImageRequestHandler(cvPixelBuffer: pixels, orientation: .up, options: [:])
        try? handler.perform([request])
        frames += 1

        var closed = 0.0, conf = 0.0, ratio = 0.0
        if let face = request.results?.first, let marks = face.landmarks {
            let left = openness(marks.leftEye, imageSize: image), right = openness(marks.rightEye, imageSize: image)
            let both = [left, right].compactMap { $0 }
            if !both.isEmpty {
                ratio = both.reduce(0, +) / Double(both.count)
                closed = scale.closed(ratio)
                // Turned well away from the camera, the far eye is foreshortened: trust it less.
                let turned = abs(face.yaw?.doubleValue ?? 0) > 0.5
                conf = Double(face.confidence) * (turned ? 0.5 : 0.95)
                if !faceSeen { faceSeen = true; log("BlinkVision: found your face.") }
            }
        }
        if conf == 0 && faceSeen && frames % 60 == 0 {
            log("BlinkVision: can't see your face. Sit facing the camera, with some light on you.")
        }
        sender.send(closed: closed, conf: conf, capture: capture, ratio: ratio, open: scale.open)

        if !quiet && capture - lastStatus > 2 {
            lastStatus = capture
            let bars = Int(closed * 10)
            log(String(format: "eyes %@%@  closed %.2f  (eye height %.3f, usually %.3f)  %.0f fps%@",
                       String(repeating: "#", count: bars), String(repeating: ".", count: 10 - bars),
                       closed, ratio, scale.open, sender.fps, conf > 0 ? "" : "  (no face)"))
        }
    }
}

// ---- main ------------------------------------------------------------------------------------------

let options = parseOptions()
let discovery = AVCaptureDevice.DiscoverySession(deviceTypes: [.builtInWideAngleCamera, .external], mediaType: .video, position: .unspecified)
let cameras = discovery.devices

if options.listCameras {
    for (i, c) in cameras.enumerated() { log("\(i): \(c.localizedName)") }
    exit(0)
}
if options.listFormats {
    for (i, c) in cameras.enumerated() {
        log("\(i): \(c.localizedName)")
        for f in c.formats {
            let d = CMVideoFormatDescriptionGetDimensions(f.formatDescription)
            let rates = f.videoSupportedFrameRateRanges.map { String(format: "%.0f-%.0f", $0.minFrameRate, $0.maxFrameRate) }.joined(separator: ", ")
            log("   \(d.width)x\(d.height)  \(rates) fps")
        }
    }
    exit(0)
}
guard !cameras.isEmpty else {
    log("BlinkVision: no camera found.")
    exit(2)
}
let device = cameras[min(max(0, options.cameraIndex), cameras.count - 1)]

func run() {
    let tracker = Tracker(options: options)
    do {
        try tracker.start(device: device)
    } catch {
        log("BlinkVision: couldn't start the camera: \(error.localizedDescription)")
        exit(3)
    }
    // Keep the tracker alive for the life of the process.
    objc_setAssociatedObject(NSObject.self, "tracker", tracker, .OBJC_ASSOCIATION_RETAIN)
}

switch AVCaptureDevice.authorizationStatus(for: .video) {
case .authorized:
    run()
case .notDetermined:
    log("BlinkVision: asking macOS for camera access…")
    AVCaptureDevice.requestAccess(for: .video) { granted in
        DispatchQueue.main.async {
            if granted { run() } else {
                log("BlinkVision: camera access was refused. Allow it in System Settings → Privacy & Security → Camera.")
                exit(4)
            }
        }
    }
default:
    log("BlinkVision: camera access is off for this app. Turn it on in System Settings → Privacy & Security → Camera (for Unity, or Terminal if you started it there), then try again.")
    exit(4)
}

dispatchMain()
