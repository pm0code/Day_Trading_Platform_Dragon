#!/usr/bin/env python3
"""
Comprehensive audit script to find ALL logging violations in service files.
This script analyzes C# service files for multiple types of violations.
"""

import os
import re
import json
from pathlib import Path
from collections import defaultdict
from datetime import datetime

class LoggingViolationAuditor:
    def __init__(self, base_path="."):
        self.base_path = Path(base_path)
        self.violations = []
        self.summary = {
            'total_files': 0,
            'files_with_violations': 0,
            'total_methods_missing_logging': 0,
            'violation_types': defaultdict(int)
        }
        
    def find_service_files(self):
        """Find all C# service files in Services directories"""
        service_files = []
        for services_dir in self.base_path.glob("**/Services"):
            for cs_file in services_dir.glob("*.cs"):
                # Skip test files and interfaces
                if "Test" not in str(cs_file) and "Interface" not in str(cs_file):
                    service_files.append(cs_file)
        return service_files
    
    def analyze_file(self, file_path):
        """Analyze a single file for logging violations"""
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
                
            violations = self.check_file_violations(file_path, content)
            
            if violations:
                self.violations.append({
                    'file': str(file_path),
                    'violations': violations,
                    'priority': self.calculate_priority(violations),
                    'file_size': len(content.splitlines()),
                    'project': self.extract_project_name(file_path)
                })
                self.summary['files_with_violations'] += 1
                
            self.summary['total_files'] += 1
            
        except Exception as e:
            print(f"Error analyzing {file_path}: {e}")
    
    def check_file_violations(self, file_path, content):
        """Check for various types of logging violations"""
        violations = []
        
        # Count methods
        method_patterns = [
            r'^\s*(public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?(?:Task<?[^>]*>?\s+|\w+\s+)\w+\s*\([^)]*\)\s*{',
            r'^\s*(public|private|protected|internal)\s+\w+\s*\([^)]*\)\s*{',  # constructors
        ]
        
        total_methods = 0
        for pattern in method_patterns:
            total_methods += len(re.findall(pattern, content, re.MULTILINE))
        
        # Check for LogMethodEntry violations
        log_method_entry_count = len(re.findall(r'LogMethodEntry\s*\(', content))
        log_method_exit_count = len(re.findall(r'LogMethodExit\s*\(', content))
        
        # Check for missing using statements
        has_logger_using = bool(re.search(r'using.*Microsoft\.Extensions\.Logging', content))
        has_trading_logger_using = bool(re.search(r'using.*TradingPlatform\.Core\.Logging', content))
        
        # Check for logger field
        has_logger_field = bool(re.search(r'(ILogger|ITradingLogger).*_logger', content))
        
        # Check for async methods without proper logging
        async_methods = len(re.findall(r'async\s+Task', content))
        
        # Check for try-catch blocks
        try_catch_count = len(re.findall(r'try\s*{', content))
        
        # Check for exception handling patterns
        has_exception_logging = bool(re.search(r'LogError\s*\(.*Exception', content))
        
        # Identify violations
        if total_methods > 0:
            if log_method_entry_count == 0:
                violations.append({
                    'type': 'MISSING_LOG_METHOD_ENTRY',
                    'severity': 'CRITICAL',
                    'description': f'All {total_methods} methods missing LogMethodEntry calls',
                    'affected_methods': total_methods,
                    'fix_effort': 'HIGH'
                })
                self.summary['total_methods_missing_logging'] += total_methods
                self.summary['violation_types']['MISSING_LOG_METHOD_ENTRY'] += 1
                
            if log_method_exit_count == 0:
                violations.append({
                    'type': 'MISSING_LOG_METHOD_EXIT',
                    'severity': 'CRITICAL',
                    'description': f'All {total_methods} methods missing LogMethodExit calls',
                    'affected_methods': total_methods,
                    'fix_effort': 'HIGH'
                })
                self.summary['violation_types']['MISSING_LOG_METHOD_EXIT'] += 1
                
            if log_method_entry_count > 0 and log_method_exit_count == 0:
                violations.append({
                    'type': 'INCONSISTENT_LOGGING',
                    'severity': 'HIGH',
                    'description': 'Has LogMethodEntry but missing LogMethodExit',
                    'affected_methods': total_methods - log_method_exit_count,
                    'fix_effort': 'MEDIUM'
                })
                self.summary['violation_types']['INCONSISTENT_LOGGING'] += 1
        
        # Check for missing logger infrastructure
        if not has_logger_using and not has_trading_logger_using:
            violations.append({
                'type': 'MISSING_LOGGER_USING',
                'severity': 'HIGH',
                'description': 'Missing logger using statements',
                'affected_methods': 0,
                'fix_effort': 'LOW'
            })
            self.summary['violation_types']['MISSING_LOGGER_USING'] += 1
            
        if not has_logger_field:
            violations.append({
                'type': 'MISSING_LOGGER_FIELD',
                'severity': 'HIGH',
                'description': 'Missing logger field declaration',
                'affected_methods': 0,
                'fix_effort': 'LOW'
            })
            self.summary['violation_types']['MISSING_LOGGER_FIELD'] += 1
            
        # Check for async method logging issues
        if async_methods > 0 and log_method_entry_count == 0:
            violations.append({
                'type': 'ASYNC_METHODS_NO_LOGGING',
                'severity': 'CRITICAL',
                'description': f'{async_methods} async methods without proper logging',
                'affected_methods': async_methods,
                'fix_effort': 'HIGH'
            })
            self.summary['violation_types']['ASYNC_METHODS_NO_LOGGING'] += 1
            
        # Check for exception handling without logging
        if try_catch_count > 0 and not has_exception_logging:
            violations.append({
                'type': 'EXCEPTION_HANDLING_NO_LOGGING',
                'severity': 'HIGH',
                'description': f'{try_catch_count} try-catch blocks without proper exception logging',
                'affected_methods': try_catch_count,
                'fix_effort': 'MEDIUM'
            })
            self.summary['violation_types']['EXCEPTION_HANDLING_NO_LOGGING'] += 1
            
        return violations
    
    def calculate_priority(self, violations):
        """Calculate priority based on violations and file characteristics"""
        critical_count = sum(1 for v in violations if v['severity'] == 'CRITICAL')
        high_count = sum(1 for v in violations if v['severity'] == 'HIGH')
        total_affected_methods = sum(v['affected_methods'] for v in violations)
        
        if critical_count > 0 and total_affected_methods > 20:
            return 'CRITICAL'
        elif critical_count > 0 or (high_count > 0 and total_affected_methods > 10):
            return 'HIGH'
        elif high_count > 0 or total_affected_methods > 5:
            return 'MEDIUM'
        else:
            return 'LOW'
    
    def extract_project_name(self, file_path):
        """Extract project name from file path"""
        path_parts = str(file_path).split('/')
        for part in path_parts:
            if part.startswith('TradingPlatform.'):
                return part
        return 'Unknown'
    
    def generate_report(self):
        """Generate comprehensive audit report"""
        report = {
            'audit_date': datetime.now().isoformat(),
            'summary': dict(self.summary),
            'violations_by_priority': self.group_violations_by_priority(),
            'violations_by_project': self.group_violations_by_project(),
            'violations_by_type': self.group_violations_by_type(),
            'detailed_violations': self.violations
        }
        
        return report
    
    def group_violations_by_priority(self):
        """Group violations by priority level"""
        by_priority = defaultdict(list)
        for violation in self.violations:
            by_priority[violation['priority']].append(violation)
        return dict(by_priority)
    
    def group_violations_by_project(self):
        """Group violations by project"""
        by_project = defaultdict(list)
        for violation in self.violations:
            by_project[violation['project']].append(violation)
        return dict(by_project)
    
    def group_violations_by_type(self):
        """Group violations by type"""
        by_type = defaultdict(list)
        for violation in self.violations:
            for v in violation['violations']:
                by_type[v['type']].append({
                    'file': violation['file'],
                    'project': violation['project'],
                    'severity': v['severity'],
                    'affected_methods': v['affected_methods']
                })
        return dict(by_type)
    
    def print_summary_report(self):
        """Print a summary report to console"""
        print("=" * 80)
        print("COMPREHENSIVE LOGGING VIOLATIONS AUDIT")
        print("=" * 80)
        print(f"Audit Date: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
        print()
        
        print("SUMMARY:")
        print(f"  Total service files analyzed: {self.summary['total_files']}")
        print(f"  Files with violations: {self.summary['files_with_violations']}")
        print(f"  Total methods missing logging: {self.summary['total_methods_missing_logging']}")
        print(f"  Violation percentage: {(self.summary['files_with_violations'] / max(self.summary['total_files'], 1)) * 100:.1f}%")
        print()
        
        print("VIOLATION TYPES:")
        for vtype, count in self.summary['violation_types'].items():
            print(f"  {vtype}: {count} files")
        print()
        
        # Print top violations by priority
        violations_by_priority = self.group_violations_by_priority()
        for priority in ['CRITICAL', 'HIGH', 'MEDIUM', 'LOW']:
            if priority in violations_by_priority:
                print(f"{priority} PRIORITY VIOLATIONS ({len(violations_by_priority[priority])} files):")
                for violation in violations_by_priority[priority][:5]:  # Show top 5
                    print(f"  {violation['file']}")
                    for v in violation['violations']:
                        if v['severity'] == priority:
                            print(f"    - {v['type']}: {v['description']}")
                print()
        
        # Print project breakdown
        violations_by_project = self.group_violations_by_project()
        print("VIOLATIONS BY PROJECT:")
        for project, violations in sorted(violations_by_project.items()):
            total_methods = sum(sum(v['affected_methods'] for v in violation['violations']) for violation in violations)
            print(f"  {project}: {len(violations)} files, {total_methods} methods")
        print()
        
        print("IMMEDIATE ACTION REQUIRED:")
        critical_files = [v for v in self.violations if v['priority'] == 'CRITICAL']
        print(f"  {len(critical_files)} files need immediate attention")
        for file_violation in critical_files[:10]:  # Show top 10
            methods_count = sum(v['affected_methods'] for v in file_violation['violations'])
            print(f"    {file_violation['file']}: {methods_count} methods")
        print()
        
        print("ESTIMATED FIX EFFORT:")
        high_effort = sum(1 for v in self.violations for violation in v['violations'] if violation['fix_effort'] == 'HIGH')
        medium_effort = sum(1 for v in self.violations for violation in v['violations'] if violation['fix_effort'] == 'MEDIUM')
        low_effort = sum(1 for v in self.violations for violation in v['violations'] if violation['fix_effort'] == 'LOW')
        print(f"  High effort fixes: {high_effort}")
        print(f"  Medium effort fixes: {medium_effort}")
        print(f"  Low effort fixes: {low_effort}")
        print("=" * 80)

def main():
    """Main execution function"""
    auditor = LoggingViolationAuditor()
    
    print("Finding service files...")
    service_files = auditor.find_service_files()
    print(f"Found {len(service_files)} service files to analyze")
    
    print("Analyzing files...")
    for i, file_path in enumerate(service_files):
        if i % 10 == 0:
            print(f"Progress: {i}/{len(service_files)}")
        auditor.analyze_file(file_path)
    
    print("\nAnalysis complete. Generating report...")
    
    # Print summary to console
    auditor.print_summary_report()
    
    # Save detailed report to JSON
    report = auditor.generate_report()
    with open('logging_violations_audit.json', 'w') as f:
        json.dump(report, f, indent=2)
    
    print(f"\nDetailed report saved to: logging_violations_audit.json")

if __name__ == "__main__":
    main()