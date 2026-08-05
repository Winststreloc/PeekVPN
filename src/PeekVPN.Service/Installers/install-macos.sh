#!/usr/bin/env bash
set -e

INSTALL_DIR=/opt/peekvpn
PLIST_NAME=com.peekvpn.service
PLIST_FILE="$PLIST_NAME.plist"

if [ "$EUID" -ne 0 ]; then
  echo "Please run as root (e.g., sudo ./install-macos.sh)"
  exit 1
fi

echo "Installing PeekVPN service to $INSTALL_DIR..."
mkdir -p "$INSTALL_DIR"
mkdir -p /var/log/peekvpn
cp -r ./publish/osx-x64/* "$INSTALL_DIR/"
chmod +x "$INSTALL_DIR/PeekVPN.Service"

echo "Installing launchd plist..."
cp "./Installers/$PLIST_FILE" "/Library/LaunchDaemons/$PLIST_FILE"
launchctl load "/Library/LaunchDaemons/$PLIST_FILE"
launchctl start "$PLIST_NAME"

echo "PeekVPN service installed and started."
echo "Check status: sudo launchctl list | grep $PLIST_NAME"
