#!/usr/bin/env bash
set -e

SERVICE_NAME=peekvpn
INSTALL_DIR=/opt/peekvpn
SERVICE_FILE=peekvpn.service

if [ "$EUID" -ne 0 ]; then
  echo "Please run as root (e.g., sudo ./install-linux.sh)"
  exit 1
fi

echo "Installing PeekVPN service to $INSTALL_DIR..."
mkdir -p "$INSTALL_DIR"
cp -r ./publish/linux-x64/* "$INSTALL_DIR/"
chmod +x "$INSTALL_DIR/PeekVPN.Service"

echo "Installing systemd unit..."
cp "./Installers/$SERVICE_FILE" "/etc/systemd/system/$SERVICE_FILE"
systemctl daemon-reload
systemctl enable "$SERVICE_NAME"
systemctl start "$SERVICE_NAME"

echo "PeekVPN service installed and started."
echo "Check status: sudo systemctl status $SERVICE_NAME"
