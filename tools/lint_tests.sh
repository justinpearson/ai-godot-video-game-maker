#!/usr/bin/env bash
#
# Lint gdUnit4 test files for problematic patterns (macOS/Linux compatible)
#
# Usage: ./tools/lint_tests.sh
#
# Exit codes:
#   0 = All checks passed
#   1 = Warnings found (review recommended)
#   2 = Errors found (must fix)
#
# This script detects:
#   - Missing extends GdUnitTestSuite
#   - Tests without assertions
#   - Potential infinite loops
#   - Test methods that lack test_ prefix

set -euo pipefail

EXIT_CODE=0
ERRORS=()
WARNINGS=()

add_error() {
    local file="$1" line="$2" message="$3"
    ERRORS+=("$file:$line: $message")
    EXIT_CODE=2
}

add_warning() {
    local file="$1" line="$2" message="$3"
    WARNINGS+=("$file:$line: $message")
    if [[ $EXIT_CODE -lt 1 ]]; then
        EXIT_CODE=1
    fi
}

echo "Linting gdUnit4 test files..."

# Find all test files
TEST_FILES=()
for dir in test/unit test/integration test; do
    if [[ -d "$dir" ]]; then
        while IFS= read -r -d '' file; do
            TEST_FILES+=("$file")
        done < <(find "$dir" -name "test_*.gd" -print0 2>/dev/null)
    fi
done

if [[ ${#TEST_FILES[@]} -eq 0 ]]; then
    echo "No test files found (looking for test_*.gd in test/)"
    exit 0
fi

echo "  Found ${#TEST_FILES[@]} test file(s)"

# Check each test file
for file in "${TEST_FILES[@]}"; do
    basename=$(basename "$file")
    content=$(cat "$file")

    # Pattern 1: Check for extends GdUnitTestSuite
    if ! echo "$content" | grep -qE 'extends\s+GdUnitTestSuite'; then
        add_error "$basename" 1 "Test file must 'extends GdUnitTestSuite'"
    fi

    # Pattern 2: Check for test_ methods
    if ! echo "$content" | grep -qE 'func\s+test_'; then
        add_warning "$basename" 1 "No test methods found (must start with 'test_')"
    fi

    # Pattern 3: Infinite loops
    line_num=0
    while IFS= read -r line; do
        line_num=$((line_num + 1))
        if echo "$line" | grep -qE 'while\s+true\s*:'; then
            add_error "$basename" "$line_num" "Infinite loop 'while true:' detected - ensure break condition exists"
        fi
    done < "$file"

    # Pattern 4: Check for assertions in test methods (simple check)
    if echo "$content" | grep -qE 'func\s+test_'; then
        # Extract test function bodies and check for assertions
        # This is a simplified check - looks for any assertion patterns in the file
        if ! echo "$content" | grep -qE 'assert_bool|assert_int|assert_float|assert_str|assert_array|assert_dict|assert_object|assert_signal|assert_vector|assert_that|assert_file|assert_result|assert_failure|fail\('; then
            add_warning "$basename" 0 "File has test methods but no assertions found"
        fi
    fi
done

# Report results
echo ""
if [[ ${#ERRORS[@]} -gt 0 ]]; then
    echo "ERRORS (${#ERRORS[@]}):"
    for err in "${ERRORS[@]}"; do
        echo "  $err"
    done
    echo ""
fi

if [[ ${#WARNINGS[@]} -gt 0 ]]; then
    echo "WARNINGS (${#WARNINGS[@]}):"
    for warn in "${WARNINGS[@]}"; do
        echo "  $warn"
    done
    echo ""
fi

if [[ $EXIT_CODE -eq 0 ]]; then
    echo "All test lint checks passed!"
else
    echo "Test lint found issues."
fi

exit $EXIT_CODE
