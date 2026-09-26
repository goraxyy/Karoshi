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
//   {"seq":1234,"closed":0.93,"conf":0.88,"src":"vision","capture":…,"sent":…,"fps":30.0}

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
        case "--quiet": o.quiet = true
        case "-h", "--help":
            print("""
            BlinkVision — webcam blink tracking for Karoshi (macOS, Apple Vision).
              --port N         UDP port the game listens on (default 5066)
              --fps N          camera frame rate to ask for (default 60)
              --camera N       which camera (see --list-cameras; default 0)
              --list-cameras   print the cameras and exit
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

    func send(closed: Double, conf: Double, capture: Double) {
        let now = Date().timeIntervalSince1970
        if last > 0 { let dt = now - last; if dt > 0 { fps = fps == 0 ? 1 / dt : fps * 0.9 + 0.1 / dt } }
        last = now
        seq += 1
        let c = max(0, min(1, closed)), k = max(0, min(1, conf))
        let json = String(format: "{\"seq\":%d,\"closed\":%.4f,\"conf\":%.3f,\"src\":\"vision\",\"capture\":%.4f,\"sent\":%.4f,\"fps\":%.1f}",
                          seq, c, k, capture, Date().timeIntervalSince1970, fps)
        connection.send(content: json.data(using: .utf8), completion: .idempotent)
    }
}

// ---- how shut the eyes are ------------------------------------------------------------------------

// Eye openness as height ÷ width of the eye's outline; the "open" reference is a high
// percentile of the recent past, so it adapts to your face, your camera and the light
// without any tuning. The game calibrates again on top of this (F9).
final class EyeScale {
    private var history: [Double] = []

    func closed(_ ratio: Double) -> Double {
        history.append(ratio)
        if history.count > 300 { history.removeFirst(history.count - 300) }
        let sorted = history.sorted()
        let open = sorted[Int(Double(sorted.count - 1) * 0.9)]
        let shut = open * 0.35
        guard open - shut > 1e-4 else { return 0 }
        return max(0, min(1, (open - ratio) / (open - shut)))
    }
}

func openness(_ region: VNFaceLandmarkRegion2D?) -> Double? {
    guard let r = region, r.pointCount >= 4 else { return nil }
    let pts = r.normalizedPoints
    var minX = Double.greatestFiniteMagnitude, maxX = -Double.greatestFiniteMagnitude
    var minY = Double.greatestFiniteMagnitude, maxY = -Double.greatestFiniteMagnitude
    for p in pts {
        minX = min(minX, Double(p.x)); maxX = max(maxX, Double(p.x))
        minY = min(minY, Double(p.y)); maxY = max(maxY, Double(p.y))
    }
    let w = maxX - minX
    return w > 1e-5 ? (maxY - minY) / w : nil
}

// ---- the camera -----------------------------------------------------------------------------------

final class Tracker: NSObject, AVCaptureVideoDataOutputSampleBufferDelegate {
    let session = AVCaptureSession()
    let sender: Sender
    let scale = EyeScale()
    let quiet: Bool
    let queue = DispatchQueue(label: "blink.camera")
    var lastStatus = 0.0
    var frames = 0
    var faceSeen = false

    init(options: Options) {
        sender = Sender(port: options.port)
        quiet = options.quiet
    }

    func start(device: AVCaptureDevice, fps: Double) throws {
        session.beginConfiguration()
        session.sessionPreset = .vga640x480
        let input = try AVCaptureDeviceInput(device: device)
        guard session.canAddInput(input) else { throw NSError(domain: "BlinkVision", code: 1, userInfo: [NSLocalizedDescriptionKey: "can't use this camera"]) }
        session.addInput(input)

        // Ask for the highest frame rate the camera offers up to `fps`: a faster camera
        // catches the start of a blink sooner.
        try? device.lockForConfiguration()
        for range in device.activeFormat.videoSupportedFrameRateRanges where range.maxFrameRate >= 30 {
            let target = min(fps, range.maxFrameRate)
            device.activeVideoMinFrameDuration = CMTime(value: 1, timescale: CMTimeScale(target))
            device.activeVideoMaxFrameDuration = CMTime(value: 1, timescale: CMTimeScale(target))
            break
        }
        device.unlockForConfiguration()

        let output = AVCaptureVideoDataOutput()
        output.alwaysDiscardsLateVideoFrames = true
        output.setSampleBufferDelegate(self, queue: queue)
        guard session.canAddOutput(output) else { throw NSError(domain: "BlinkVision", code: 2, userInfo: [NSLocalizedDescriptionKey: "can't read frames"]) }
        session.addOutput(output)
        session.commitConfiguration()
        session.startRunning()
        log("BlinkVision: camera '\(device.localizedName)' started — sending to 127.0.0.1. Frames are not saved.")
    }

    func captureOutput(_ output: AVCaptureOutput, didOutput sampleBuffer: CMSampleBuffer, from connection: AVCaptureConnection) {
        let capture = Date().timeIntervalSince1970
        guard let pixels = CMSampleBufferGetImageBuffer(sampleBuffer) else { return }
        let request = VNDetectFaceLandmarksRequest()
        let handler = VNImageRequestHandler(cvPixelBuffer: pixels, orientation: .up, options: [:])
        try? handler.perform([request])
        frames += 1

        var closed = 0.0, conf = 0.0
        if let face = request.results?.first, let marks = face.landmarks,
           let left = openness(marks.leftEye), let right = openness(marks.rightEye) {
            closed = scale.closed((left + right) / 2)
            conf = Double(face.confidence) * 0.95
            if !faceSeen { faceSeen = true; log("BlinkVision: found your face.") }
        } else if faceSeen && frames % 60 == 0 {
            log("BlinkVision: can't see your face — sit facing the camera, with some light on you.")
        }
        sender.send(closed: closed, conf: conf, capture: capture)

        if !quiet && capture - lastStatus > 2 {
            lastStatus = capture
            let bars = Int(closed * 10)
            log(String(format: "eyes %@%@  closed %.2f  %.0f fps%@", String(repeating: "#", count: bars), String(repeating: ".", count: 10 - bars),
                       closed, sender.fps, conf > 0 ? "" : "  (no face)"))
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
guard !cameras.isEmpty else {
    log("BlinkVision: no camera found.")
    exit(2)
}
let device = cameras[min(max(0, options.cameraIndex), cameras.count - 1)]

func run() {
    let tracker = Tracker(options: options)
    do {
        try tracker.start(device: device, fps: options.fps)
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
