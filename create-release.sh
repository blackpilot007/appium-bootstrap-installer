#!/bin/bash
#
# Copyright 2025 Appium Bootstrap Installer Contributors
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#

# Create a new release of Appium Bootstrap Installer
#
# Usage:
#   ./create-release.sh [--type major|minor|patch] [--version x.y.z] [--dry-run]
#
# Examples:
#   ./create-release.sh --type patch       # Increment patch version
#   ./create-release.sh --version 1.0.0    # Create specific version
#   ./create-release.sh --type minor --dry-run  # Preview changes

set -e

# Default values
RELEASE_TYPE="patch"
VERSION=""
DRY_RUN=false

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m' # No Color

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --type|-t)
            RELEASE_TYPE="$2"
            shift 2
            ;;
        --version|-v)
            VERSION="$2"
            shift 2
            ;;
        --dry-run|-d)
            DRY_RUN=true
            shift
            ;;
        --help|-h)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --type, -t TYPE       Release type: major, minor, or patch (default: patch)"
            echo "  --version, -v VERSION Specific version to use (overrides --type)"
            echo "  --dry-run, -d         Show what would be done without making changes"
            echo "  --help, -h            Show this help message"
            echo ""
            echo "Examples:"
            echo "  $0 --type patch"
            echo "  $0 --version 1.0.0"
            echo "  $0 --type minor --dry-run"
            exit 0
            ;;
        *)
            echo -e "${RED}❌ Unknown option: $1${NC}"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Validate release type
if [[ ! "$RELEASE_TYPE" =~ ^(major|minor|patch)$ ]]; then
    echo -e "${RED}❌ Error: Invalid release type: $RELEASE_TYPE${NC}"
    echo "   Must be one of: major, minor, patch"
    exit 1
fi

# Functions
get_current_version() {
    local version_file="publish/VERSION"
    if [ ! -f "$version_file" ]; then
        echo -e "${RED}❌ Error: VERSION file not found at: $version_file${NC}"
        exit 1
    fi
    
    cat "$version_file" | tr -d '[:space:]'
}

get_next_version() {
    local current_version=$1
    local release_type=$2
    
    IFS='.' read -ra VER <<< "$current_version"
    if [ ${#VER[@]} -ne 3 ]; then
        echo -e "${RED}❌ Error: Invalid version format: $current_version${NC}"
        echo "   Expected format: major.minor.patch"
        exit 1
    fi
    
    local major=${VER[0]}
    local minor=${VER[1]}
    local patch=${VER[2]}
    
    case $release_type in
        major)
            major=$((major + 1))
            minor=0
            patch=0
            ;;
        minor)
            minor=$((minor + 1))
            patch=0
            ;;
        patch)
            patch=$((patch + 1))
            ;;
    esac
    
    echo "$major.$minor.$patch"
}

test_git_repository() {
    if ! git rev-parse --git-dir > /dev/null 2>&1; then
        echo -e "${RED}❌ Error: Not a git repository${NC}"
        exit 1
    fi
}

test_clean_working_directory() {
    if [ -n "$(git status --porcelain)" ]; then
        echo -e "${YELLOW}⚠️  Warning: You have uncommitted changes${NC}"
        read -p "Continue anyway? (y/N) " -n 1 -r
        echo
        if [[ ! $REPLY =~ ^[Yy]$ ]]; then
            echo -e "${YELLOW}Aborted.${NC}"
            exit 0
        fi
    fi
}

update_version_file() {
    local new_version=$1
    local version_file="publish/VERSION"
    
    if [ "$DRY_RUN" = true ]; then
        echo -e "  ${YELLOW}[DRY RUN] Would update $version_file to: $new_version${NC}"
    else
        echo -n "$new_version" > "$version_file"
        echo -e "  ${GREEN}✓ Updated $version_file to: $new_version${NC}"
    fi
}

create_git_tag() {
    local version=$1
    local tag="v$version"
    
    if [ "$DRY_RUN" = true ]; then
        echo -e "  ${YELLOW}[DRY RUN] Would create and push tag: $tag${NC}"
    else
        # Commit VERSION file change
        git add publish/VERSION
        git commit -m "chore: bump version to $version"
        
        # Create annotated tag
        git tag -a "$tag" -m "Release $tag"
        
        # Push commit and tag
        git push origin HEAD
        git push origin "$tag"
        
        echo -e "  ${GREEN}✓ Created and pushed tag: $tag${NC}"
    fi
}

# Main script
echo -e "\n${CYAN}🚀 Appium Bootstrap Installer - Release Creator${NC}\n"

# Verify git repository
test_git_repository

# Check for uncommitted changes
test_clean_working_directory

# Get current version
CURRENT_VERSION=$(get_current_version)
echo -e "${NC}📦 Current version: $CURRENT_VERSION${NC}"

# Determine new version
if [ -n "$VERSION" ]; then
    NEW_VERSION="$VERSION"
    echo -e "${CYAN}🎯 Target version: $NEW_VERSION (manual)${NC}"
else
    NEW_VERSION=$(get_next_version "$CURRENT_VERSION" "$RELEASE_TYPE")
    echo -e "${CYAN}🎯 Target version: $NEW_VERSION ($RELEASE_TYPE release)${NC}"
fi

# Validate version format
if [[ ! "$NEW_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo -e "${RED}❌ Error: Invalid version format: $NEW_VERSION${NC}"
    echo "   Expected format: major.minor.patch (e.g., 1.0.0)"
    exit 1
fi

echo -e "\n${NC}Release plan:${NC}"
echo -e "  ${GRAY}Current:  $CURRENT_VERSION${NC}"
echo -e "  ${GREEN}New:      $NEW_VERSION${NC}"
echo -e "  ${GREEN}Tag:      v$NEW_VERSION${NC}"

if [ "$DRY_RUN" = false ]; then
    echo -e "\n${YELLOW}⚠️  This will:${NC}"
    echo -e "   ${YELLOW}1. Update publish/VERSION${NC}"
    echo -e "   ${YELLOW}2. Commit the change${NC}"
    echo -e "   ${YELLOW}3. Create and push tag v$NEW_VERSION${NC}"
    echo -e "   ${YELLOW}4. Trigger GitHub Actions release workflow${NC}"
    
    read -p "$(echo -e "\nProceed with release? (y/N) ")" -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo -e "${YELLOW}Aborted.${NC}"
        exit 0
    fi
fi

echo -e "\n${CYAN}📝 Creating release...${NC}\n"

# Update VERSION file
update_version_file "$NEW_VERSION"

# Create and push git tag
create_git_tag "$NEW_VERSION"

if [ "$DRY_RUN" = true ]; then
    echo -e "\n${GREEN}✅ Dry run completed successfully!${NC}"
    echo -e "\n${CYAN}To create the release for real, run:${NC}"
    if [ -n "$VERSION" ]; then
        echo -e "  ${NC}./create-release.sh --version $VERSION${NC}"
    else
        echo -e "  ${NC}./create-release.sh --type $RELEASE_TYPE${NC}"
    fi
else
    # Get repository URL
    REPO_URL=$(git config --get remote.origin.url | sed 's/.*github\.com[:/]\(.*\)\.git/\1/')
    
    echo -e "\n${GREEN}✅ Release v$NEW_VERSION created successfully!${NC}"
    echo -e "\n${CYAN}📋 Next steps:${NC}"
    echo -e "  ${NC}1. GitHub Actions will build for all platforms${NC}"
    echo -e "  ${NC}2. Release will be published at:${NC}"
    echo -e "     ${NC}https://github.com/$REPO_URL/releases/tag/v$NEW_VERSION${NC}"
    echo -e "  ${NC}3. Monitor the workflow at:${NC}"
    echo -e "     ${NC}https://github.com/$REPO_URL/actions${NC}"
fi
