# Example unit test demonstrating GUT usage.
extends GutTest


func test_example_passes() -> void:
	assert_true(true, "This test should always pass")


func test_example_equality() -> void:
	var expected := 42
	var actual := 40 + 2
	assert_eq(actual, expected)
