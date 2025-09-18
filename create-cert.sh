#!/bin/bash

# create-cert.sh - Generate self-signed certificate for Booqr API

set -e

# Configuration
CERT_DIR="./certs"
CERT_NAME="booqr"
CERT_SUBJECT="/CN=localhost/O=Booqr/C=DK"
DAYS_VALID=1000000

# Load environment variables from .env file
if [ -f ".env" ]; then
    export $(cat .env | grep -v '^#' | xargs)
else
    echo -e "${RED}Error: .env file not found. Please create one with CERT_PASSWORD variable.${NC}"
    echo "Example: cp .env.example .env"
    exit 1
fi

# Check if password is set
if [ -z "$CERT_PASSWORD" ]; then
    echo -e "${RED}Error: CERT_PASSWORD not set in .env file.${NC}"
    exit 1
fi

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${YELLOW}Creating self-signed certificate for Booqr API...${NC}"

# Create certs directory if it doesn't exist
if [ ! -d "$CERT_DIR" ]; then
    echo "Creating certificates directory: $CERT_DIR"
    mkdir -p "$CERT_DIR"
fi

# Check if certificate already exists
if [ -f "$CERT_DIR/$CERT_NAME.pfx" ]; then
    echo -e "${YELLOW}Certificate already exists. Overwriting...${NC}"
    rm -f "$CERT_DIR/$CERT_NAME".*
fi

# Generate private key
echo "Generating private key..."
openssl genrsa -out "$CERT_DIR/$CERT_NAME.key" 2048

# Generate certificate signing request
echo "Generating certificate signing request..."
openssl req -new -key "$CERT_DIR/$CERT_NAME.key" -out "$CERT_DIR/$CERT_NAME.csr" -subj "$CERT_SUBJECT"

# Generate self-signed certificate
echo "Generating self-signed certificate (valid for $DAYS_VALID days)..."
openssl x509 -req -in "$CERT_DIR/$CERT_NAME.csr" -signkey "$CERT_DIR/$CERT_NAME.key" -out "$CERT_DIR/$CERT_NAME.crt" -days $DAYS_VALID

# Convert to PFX format for .NET
echo "Converting to PFX format..."
openssl pkcs12 -export -out "$CERT_DIR/$CERT_NAME.pfx" -inkey "$CERT_DIR/$CERT_NAME.key" -in "$CERT_DIR/$CERT_NAME.crt" -passout pass:$CERT_PASSWORD

# Set appropriate permissions
chmod 644 "$CERT_DIR/$CERT_NAME.pfx"
chmod 644 "$CERT_DIR/$CERT_NAME.crt"
chmod 600 "$CERT_DIR/$CERT_NAME.key"

# Clean up intermediate files
rm "$CERT_DIR/$CERT_NAME.csr"

# Install certificate to system trust store (requires sudo)
echo -e "${YELLOW}Installing certificate to system trust store...${NC}"
if command -v update-ca-certificates >/dev/null 2>&1; then
    # Debian/Ubuntu
    if [ "$(id -u)" -eq 0 ]; then
        cp "$CERT_DIR/$CERT_NAME.crt" /usr/local/share/ca-certificates/booqr.crt
        update-ca-certificates
        echo -e "${GREEN}✓ Certificate installed to system trust store${NC}"
    else
        echo -e "${YELLOW}To install certificate to system trust store, run:${NC}"
        echo "sudo cp $CERT_DIR/$CERT_NAME.crt /usr/local/share/ca-certificates/booqr.crt"
        echo "sudo update-ca-certificates"
    fi
elif command -v trust >/dev/null 2>&1; then
    # Fedora/RHEL/CentOS
    if [ "$(id -u)" -eq 0 ]; then
        trust anchor --store "$CERT_DIR/$CERT_NAME.crt"
        echo -e "${GREEN}✓ Certificate installed to system trust store${NC}"
    else
        echo -e "${YELLOW}To install certificate to system trust store, run:${NC}"
        echo "sudo trust anchor --store $CERT_DIR/$CERT_NAME.crt"
    fi
else
    echo -e "${YELLOW}Manual certificate installation required for this system${NC}"
fi

# Set proper permissions for container user
chmod 644 "$CERT_DIR/$CERT_NAME.pfx"
chmod 644 "$CERT_DIR/$CERT_NAME.crt"
chmod 600 "$CERT_DIR/$CERT_NAME.key"

echo -e "${GREEN}Certificate created successfully!${NC}"
echo "Files created:"
echo "  - $CERT_DIR/$CERT_NAME.pfx (PFX certificate for Kestrel)"
echo "  - $CERT_DIR/$CERT_NAME.crt (Certificate file)"
echo "  - $CERT_DIR/$CERT_NAME.key (Private key)"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo "1. Run: docker-compose up --build"
echo "2. Access your API at: https://localhost:8443"
echo ""
echo -e "${YELLOW}Benefits of installing to system trust store:${NC}"
echo "• Host can make HTTPS calls to the container without SSL warnings"
echo "• Health checks work properly"
echo "• Integration tests can run from host"
echo "• Browser won't show security warnings for localhost"
echo ""
echo -e "${RED}Note: This is a self-signed certificate. Browsers will show security warnings.${NC}"
