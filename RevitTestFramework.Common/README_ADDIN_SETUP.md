# Revit Test Framework - Addin Setup

This document explains how to set up the Revit Test Framework addins for Revit.

## Automatic Installation

The easiest way to install the addin manifests is to use the command-line tool provided in RevitTestFramework.Common:
# Install both XUnit and NUnit addins for Revit 2025 (default)
RevitTestFramework.Common.exe generate-all-manifests

# Install for a specific Revit version
RevitTestFramework.Common