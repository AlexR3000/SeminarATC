import socket
import time

def main():
    print("hello world")
    server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sample_provider(server_socket, "127.0.0.1", 5678)
    server_socket.close()

def sample_provider(server_socket, host, port):

    client_socket, client_address = wait_for_request(server_socket, host, port)
    print("Connection accepted")

    try:
        print("sending...")
        sample_messages = read_file()

        for message in sample_messages:
            send_sample(client_socket, message)
            time.sleep(0.0001)

    except Exception as e:
        server_socket.close()
        client_socket.close()
        print(e)

def send_sample(client_socket, message):
    client_socket.send(message)

def read_file():
    with open('recording-08-11-23-1701.txt', 'r') as file:
        lines = file.readlines()
        for line in lines:
            yield line.encode('utf-8')
    

def wait_for_request(server_socket, host, port):

    server_socket.bind((host, port))

    server_socket.listen(1)
    return server_socket.accept()




if __name__ == "__main__":
    main()