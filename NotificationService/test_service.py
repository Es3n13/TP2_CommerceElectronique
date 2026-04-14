import requests
import os
import time
import subprocess
import signal

SERVICE_PATH = "/root/.openclaw/workspace/TP2_CommerceElectronique_V.Alpha/NotificationService/NotificationService.csproj"
BASE_URL = "http://localhost:5005"
INBOX_ROOT = "/root/.openclaw/workspace/notifications/inbox"

def test_notification_service():
    user_id = "test_user_123"
    message = "Hello, this is a test notification!"
    notif_type = "TestType"

    print(f"--- Testing POST /notify ---")
    payload = {
        "userId": user_id,
        "message": message,
        "type": notif_type
    }
    
    try:
        resp = requests.post(f"{BASE_URL}/notify", json=payload)
        resp.raise_for_status()
        data = resp.json()
        notif_id = data.get("id")
        print(f"Successfully notified. ID: {notif_id}")
    except Exception as e:
        print(f"POST /notify failed: {e}")
        return False

    print(f"--- Verifying HTML File ---")
    file_path = os.path.join(INBOX_ROOT, user_id, f"{notif_id}.html")
    if os.path.exists(file_path):
        print(f"HTML file exists at: {file_path}")
        with open(file_path, 'r') as f:
            content = f.read()
            if message in content:
                print("HTML content is correct.")
            else:
                print("HTML content missing message!")
                return False
    else:
        print(f"HTML file NOT found at: {file_path}")
        return False

    print(f"--- Testing GET /inbox/{user_id} ---")
    try:
        resp = requests.get(f"{BASE_URL}/inbox/{user_id}")
        resp.raise_for_status()
        inbox = resp.json()
        if any(n.get("id") == notif_id for n in inbox):
            print(f"Notification {notif_id} found in inbox.")
        else:
            print(f"Notification {notif_id} NOT found in inbox.")
            return False
    except Exception as e:
        print(f"GET /inbox failed: {e}")
        return False

    print(f"--- Testing GET /view/{notif_id} ---")
    try:
        resp = requests.get(f"{BASE_URL}/view/{notif_id}")
        resp.raise_for_status()
        if "text/html" in resp.headers.get("Content-Type", ""):
            print("Response content-type is text/html.")
            if message in resp.text:
                print("HTML content verified via API.")
            else:
                print("API returned HTML but message missing!")
                return False
        else:
            print(f"Wrong content-type: {resp.headers.get('Content-Type')}")
            return False
    except Exception as e:
        print(f"GET /view failed: {e}")
        return False

    print("\n✅ ALL TESTS PASSED")
    return True

if __name__ == "__main__":
    # 1. Start the service
    print("Starting Notification Service...")
    process = subprocess.Popen(
        ["dotnet", "run", "--project", SERVICE_PATH],
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True
    )

    # Wait for service to start (polling /swagger or just sleeping)
    # We'll wait for the "Now listening on" message in stdout
    timeout = 30
    start_time = time.time()
    while time.time() - start_time < timeout:
        # This is a bit naive as we don't have a good way to read Popen stdout non-blockingly without threads
        # but for a simple test we can just sleep.
        time.sleep(2)
        try:
            requests.get(f"{BASE_URL}/notify") # Try a request to see if it's up (should be 405/404 but not connection error)
            print("Service is up!")
            break
        except requests.exceptions.ConnectionError:
            pass
    else:
        print("Service failed to start within timeout.")
        process.terminate()
        exit(1)

    # 2. Run tests
    success = test_notification_service()

    # 3. Cleanup
    print("Stopping Notification Service...")
    process.terminate()
    
    if success:
        exit(0)
    else:
        exit(1)
