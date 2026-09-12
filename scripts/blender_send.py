"""Send commands to Blender MCP addon via socket."""
import socket
import json
import sys

HOST = "localhost"
PORT = 9876

def send_command(cmd_type, params=None):
    """Send a JSON command to Blender and return the response."""
    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    sock.settimeout(10)
    sock.connect((HOST, PORT))
    
    message = json.dumps({"type": cmd_type, "params": params or {}})
    sock.sendall((message + "\n").encode("utf-8"))
    
    # Read response
    response = b""
    while True:
        try:
            chunk = sock.recv(4096)
            if not chunk:
                break
            response += chunk
            if b"\n" in response:
                break
        except socket.timeout:
            break
    
    sock.close()
    
    if response:
        return json.loads(response.decode("utf-8").strip())
    return {"status": "error", "message": "no response"}

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python blender_send.py <command_type> [json_params]")
        print("Example: python blender_send.py execute_blender_code '{\"code\": \"print(123)\"}'")
        sys.exit(1)
    
    cmd = sys.argv[1]
    params = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
    result = send_command(cmd, params)
    print(json.dumps(result, indent=2))
