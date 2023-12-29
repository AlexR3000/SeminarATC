#  SBS importer

import socket

HOST = "141.79.10.172"  # Raspi Dachantenne B-Gebäude
PORT = 30003  # SBS  message port

BLOCK_MAX = 80641 # number of blocks to be received

s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)   

s.connect((HOST, PORT))
print ("Connection established")

blk_cnt = 0
msg_cnt = 0

test_count = 0
stop = True
while stop:
    data = str(s.recv(100000)) 
    #print(f"----------Received Block " +  str(blk_cnt)
    #                  + " with len " + str(len(data))
    #                  + "\nUnparsed Contents: \n" + data + "\n")
    blk_cnt = blk_cnt + 1

    contents = data[2:-1]  #byte-string-lexicals
    msg_list = contents.split("\\r\\n")
    for msg in msg_list:
        if msg == "":
            test_count = test_count + 1
            if test_count >= 10:
                input()
        else:
            test_count = 0
        print("Blk " + str(blk_cnt) + " Msg " + str(msg_cnt) + "  " + msg )
        msg_cnt = msg_cnt + 1
        
print ("done")
input()
