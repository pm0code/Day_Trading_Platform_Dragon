#!/usr/bin/env python3
"""
Phase 2: Comprehensive fix for all logging parameter issues.
Converts template-style logging calls to proper string interpolation.
"""

import os
import re
import sys
from pathlib import Path
from typing import List, Tuple, Dict
import argparse

class ComprehensiveLoggingFixer:
    def __init__(self, verbose=False):
        self.fixed_count = 0
        self.files_processed = 0
        self.files_modified = 0
        self.patterns_fixed = {}
        self.verbose = verbose
        self.errors = []
        
    def fix_logging_call(self, content: str) -> Tuple[str, int]:
        """Fix all logging calls in the content."""
        changes = 0
        
        # Define patterns for different logging methods
        logging_methods = ['LogError', 'LogInfo', 'LogWarning', 'LogDebug', 'LogTrace']
        
        for method in logging_methods:
            # Pattern to match calls like: LogMethod("Text {Placeholder}", value, ...)
            # This handles both single-line and multi-line calls
            pattern = rf'((?:TradingLogOrchestrator\.Instance\.|_logger\.){method}\()("(?:[^"\\]|\\.)*\{{[^}}]+\}}(?:[^"\\]|\\.)*")((?:\s*,\s*[^,)]+)+)(\s*\))'
            
            def replacer(match):
                nonlocal changes
                prefix = match.group(1)
                template = match.group(2)
                params = match.group(3)
                suffix = match.group(4)
                
                # Extract placeholders from template
                placeholders = re.findall(r'\{([^}]+)\}', template)
                if not placeholders:
                    return match.group(0)
                
                # Parse parameters (simplified - handles common cases)
                param_list = []
                param_str = params.strip().lstrip(',').strip()
                
                # Simple parameter splitting (doesn't handle nested parentheses perfectly)
                current_param = ""
                paren_depth = 0
                in_quotes = False
                i = 0
                
                while i < len(param_str):
                    char = param_str[i]
                    
                    if char == '"' and (i == 0 or param_str[i-1] != '\\'):
                        in_quotes = not in_quotes
                    elif char == '(' and not in_quotes:
                        paren_depth += 1
                    elif char == ')' and not in_quotes:
                        paren_depth -= 1
                    elif char == ',' and paren_depth == 0 and not in_quotes:
                        if current_param.strip():
                            param_list.append(current_param.strip())
                        current_param = ""
                        i += 1
                        continue
                    
                    current_param += char
                    i += 1
                
                if current_param.strip():
                    param_list.append(current_param.strip())
                
                # Check if last parameter is 'ex' (exception)
                has_exception = False
                exception_param = None
                if param_list and param_list[-1].strip() in ['ex', 'exception', 'e']:
                    has_exception = True
                    exception_param = param_list.pop()
                
                # Build interpolated string
                template_str = template[1:-1]  # Remove quotes
                for i, placeholder in enumerate(placeholders):
                    if i < len(param_list):
                        value = param_list[i]
                        template_str = template_str.replace(f'{{{placeholder}}}', f'{{{value}}}')
                
                # Reconstruct the call
                if has_exception:
                    result = f'{prefix}$"{template_str}", {exception_param}{suffix}'
                else:
                    result = f'{prefix}$"{template_str}"{suffix}'
                
                changes += 1
                self.patterns_fixed[method] = self.patterns_fixed.get(method, 0) + 1
                
                if self.verbose:
                    print(f"  Fixed {method} call")
                    print(f"    From: {match.group(0)}")
                    print(f"    To:   {result}")
                
                return result
            
            content = re.sub(pattern, replacer, content, flags=re.MULTILINE | re.DOTALL)
        
        return content, changes
    
    def process_file(self, filepath: Path) -> bool:
        """Process a single file."""
        try:
            with open(filepath, 'r', encoding='utf-8') as f:
                original_content = f.read()
            
            fixed_content, changes = self.fix_logging_call(original_content)
            
            if changes > 0:
                # Create backup
                backup_path = filepath.with_suffix(filepath.suffix + '.bak_phase2')
                with open(backup_path, 'w', encoding='utf-8') as f:
                    f.write(original_content)
                
                # Write fixed content
                with open(filepath, 'w', encoding='utf-8') as f:
                    f.write(fixed_content)
                
                print(f"✓ Fixed {changes} logging calls in {filepath.name}")
                self.fixed_count += changes
                self.files_modified += 1
                return True
            else:
                if self.verbose:
                    print(f"  No changes needed in {filepath.name}")
                return False
                
        except Exception as e:
            error_msg = f"Error processing {filepath}: {e}"
            print(f"✗ {error_msg}")
            self.errors.append(error_msg)
            return False
    
    def find_files_with_issues(self, root_dir: Path) -> List[Path]:
        """Find all C# files with potential logging issues."""
        files = []
        
        # Patterns to search for
        search_patterns = [
            r'Log(?:Error|Info|Warning|Debug|Trace)\s*\(\s*"[^"]*\{[^}]+\}[^"]*"\s*,',
            r'TradingLogOrchestrator\.Instance\.Log\w+\s*\(\s*"[^"]*\{[^}]+\}',
            r'_logger\.Log\w+\s*\(\s*"[^"]*\{[^}]+\}'
        ]
        
        for filepath in root_dir.rglob("*.cs"):
            # Skip backup files and hidden directories
            if any(part.startswith('.') for part in filepath.parts):
                continue
            if filepath.suffix.endswith('.bak') or filepath.suffix.endswith('.backup'):
                continue
                
            try:
                with open(filepath, 'r', encoding='utf-8') as f:
                    content = f.read()
                    
                for pattern in search_patterns:
                    if re.search(pattern, content, re.MULTILINE):
                        files.append(filepath)
                        break
                        
            except Exception as e:
                if self.verbose:
                    print(f"Error reading {filepath}: {e}")
                    
        return sorted(set(files))
    
    def run(self, root_dir: str):
        """Run the fixer on all files in the directory."""
        root_path = Path(root_dir).resolve()
        print(f"\nPhase 2: Comprehensive Logging Parameter Fix")
        print(f"Directory: {root_path}")
        print("="*70)
        
        # Find files with issues
        print("Scanning for files with logging parameter issues...")
        files = self.find_files_with_issues(root_path)
        print(f"Found {len(files)} files with potential issues\n")
        
        if not files:
            print("No files found with logging parameter issues.")
            return
        
        # Process each file
        for i, filepath in enumerate(files, 1):
            rel_path = filepath.relative_to(root_path)
            print(f"[{i}/{len(files)}] Processing {rel_path}")
            self.process_file(filepath)
            self.files_processed += 1
        
        # Print summary
        print(f"\n{'='*70}")
        print(f"Phase 2 Summary:")
        print(f"  Files processed: {self.files_processed}")
        print(f"  Files modified: {self.files_modified}")
        print(f"  Total fixes: {self.fixed_count}")
        
        if self.patterns_fixed:
            print(f"\nPatterns fixed by method:")
            for method, count in sorted(self.patterns_fixed.items()):
                print(f"  {method}: {count}")
        
        if self.errors:
            print(f"\nErrors encountered: {len(self.errors)}")
            for error in self.errors[:5]:
                print(f"  - {error}")
            if len(self.errors) > 5:
                print(f"  ... and {len(self.errors) - 5} more")
        
        print(f"{'='*70}\n")

def main():
    parser = argparse.ArgumentParser(description='Fix logging parameter order issues in C# files')
    parser.add_argument('directory', nargs='?', default='.', 
                        help='Directory to process (default: current directory)')
    parser.add_argument('-v', '--verbose', action='store_true',
                        help='Enable verbose output')
    
    args = parser.parse_args()
    
    fixer = ComprehensiveLoggingFixer(verbose=args.verbose)
    fixer.run(args.directory)

if __name__ == "__main__":
    main()