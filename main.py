import socket
from ultralytics import YOLO
from collections import namedtuple
import numpy as np
import struct
import cv2
import time
import threading
from PIL import Image
import io

print("start")
# Connect to image server from HoloLens2
IMAGE_CLIENT_HOST = "192.168.0.151"  # TODO: 改成要連接眼鏡的 IP 和 PORT
IMAGE_CLIENT_PORT = 5010
IMAGE_HEADER_FORMAT = "@IBBHqIIII"  # Image Header Format Protocol
IMAGE_HEADER_SIZE = struct.calcsize(IMAGE_HEADER_FORMAT)
IMAGE_HEADER = namedtuple(
    'SensorFrameStreamHeader',
    'Cookie VersionMajor VersionMinor FrameType Timestamp ImageWidth ImageHeight PixelStride RowStride'
)

print("Waiting for Server...")
image_client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
image_client.connect((IMAGE_CLIENT_HOST, IMAGE_CLIENT_PORT))
print(
    f'INFO: image_client connected to {IMAGE_CLIENT_HOST} on port {IMAGE_CLIENT_PORT}')

# Create Bounding Boxes Server to send prediction back
BBOXES_SERVER_HOST = "192.168.0.161"  # TODO: 改成這台電腦的 IP 和 PORT
BBOXES_SERVER_PORT = 12345
# TODO: 根據要回傳的資料型態定義 Protocol
BBOXES_HEADER_FORMAT = "@36f"  # Bboxes Header Format Protocol

bbox_server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
bbox_server.bind((BBOXES_SERVER_HOST, BBOXES_SERVER_PORT))
bbox_server.listen()
print(
    f'INFO: bboxes_server created at {BBOXES_SERVER_HOST} on port {BBOXES_SERVER_PORT}')

# Initialize Object Detection Model (YOLOv8)
MODEL_PATH = "beaker_yolov8_model.pt"  # TODO: 改成自己的模型
model = YOLO(MODEL_PATH)
names = model.names
print(f"INFO: YOLOv8 Model initialized")


# Save prediction data to send back
EMPTY_DATA_BUFFER = [-1.] * 36
data_buffer = [-1.] * 36
box_num = 6


def receive_image():
    # Receive header
    # reply = image_client.recv(IMAGE_HEADER_SIZE)
    reply = image_client.recv(4)
    # print(len(reply))
    data = struct.unpack("<I", reply)[0]
    # print(data)
    # if not reply:
    #     print('ERROR: Failed to receive data')
    #     return None
    # data = struct.unpack(IMAGE_HEADER_FORMAT, reply)
    # header = IMAGE_HEADER(*data)
    # print(data)
    # Receive image
    #image_size_bytes = header.ImageHeight * header.RowStride
    #image_size_bytes=1577600
    # image_size_bytes=1228800
    image_size_bytes = data
    image_data = bytes()
    # print(image_size_bytes)

    # print(header.ImageHeight)
    # print(header.RowStride)

    while len(image_data) < image_size_bytes:
        remaining_bytes = image_size_bytes - len(image_data)
        image_data_chunk = image_client.recv(remaining_bytes)
        if not image_data_chunk:
            print('ERROR: Failed to receive image data')
            return None
        image_data += image_data_chunk

    print("Received img data.")
    # image = np.frombuffer(image_data, dtype=np.uint8).reshape(
    #     (header.ImageHeight, header.ImageWidth, header.PixelStride))
    
    # image = np.frombuffer(image_data, dtype=np.uint8).reshape(
    #     (480,640,4))
    image = Image.open(io.BytesIO(image_data))
    image = np.array(image)
    print("轉換成功")

    image = cv2.cvtColor(image, cv2.COLOR_BGRA2BGR)
    print("真的轉換成功")

    return image


def predict(image):  # TODO: 根據你們的模型改需要送回的資料型態
    global data_buffer

    # Model predict image
    result = model(image)[0]
    data_buffer = [-1.] * 36

    # Parse results
    for i in range(len(result.boxes.cls)):
        if i >= box_num:
            break
        data_buffer[6 * i + 0] = result.boxes.cls[i]
        data_buffer[6 * i + 1] = result.boxes.xywh[i][0]
        data_buffer[6 * i + 2] = result.boxes.xywh[i][1]
        data_buffer[6 * i + 3] = result.boxes.xywh[i][3]  # Width Height 顛倒
        data_buffer[6 * i + 4] = result.boxes.xywh[i][2]
        data_buffer[6 * i + 5] = result.boxes.conf[i]


def show_image(image, fps=None):
    global data_buffer
    for i in range(box_num):
        cls = int(data_buffer[6 * i + 0])
        x = int(data_buffer[6 * i + 1])
        y = int(data_buffer[6 * i + 2])
        h = int(data_buffer[6 * i + 3] / 2)
        w = int(data_buffer[6 * i + 4] / 2)
        conf = data_buffer[6 * i + 5]
        if cls == -1:
            continue
        if conf <= 0.7:  # Threshold: 60% confidence
            continue
        label = names[cls]
        image = cv2.rectangle(image, (x - w, y - h),
                              (x + w, y + h), (0, 255, 0), 2)
        image = cv2.putText(image, f"{label} {conf:.2%}", (x - w, y - h),
                            cv2.FONT_HERSHEY_COMPLEX, 0.5, (0, 255, 0), 1)
        print(f"cls:{cls},x:{x},y:{y},w:{w},h:{h}")
    if fps is not None:
        image = cv2.putText(
            image, f"FPS: {fps:.2f}", (10, 30), cv2.FONT_HERSHEY_COMPLEX, 1, (0, 0, 0), 2)

    cv2.imshow("HoloLens 2 Object Detection", image)
    if cv2.waitKey(1) & 0xFF == ord('q'):
        cv2.destroyAllWindows()
    return


def send_bboxes(bboxes_client, bboxes_client_address):
    global data_buffer

    print(
        f'INFO: client connected from {BBOXES_SERVER_HOST}:{BBOXES_SERVER_PORT}')
    while True:
        # Sending bounding boxes
        data = struct.pack(BBOXES_HEADER_FORMAT, *data_buffer)
        bboxes_client.send(data)


def main():
    print("INFO: Start main thread")
    while True:
        start_time = time.time()
        image = receive_image()
        if image is None:
            continue
        predict(image)
        fps = 1.0 / (time.time() - start_time)
        show_image(image, fps)


if __name__ == "__main__":
    # Run main thread (receive image from HoloLens2)
    threading.Thread(target=main).start()
    # Run client thread (send bounding boxes results back to HoloLens2)
    while True:
        print("bbx client accepting")
        (bboxes_client, bboxes_client_address) = bbox_server.accept()
        threading.Thread(target=send_bboxes, args=(
            bboxes_client, bboxes_client_address)).start()
