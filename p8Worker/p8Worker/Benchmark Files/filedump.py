# Import the os and random modules
import os
import random

# Set the number of files and the number of numbers per file
num_files = 100
numbers_per_file = 1048576

# Create the files
for i in range(num_files):
    # Generate the file name
    file_name = "numbers_" + str(i) + ".txt"

    # Open the file in write mode
    with open(file_name, "w") as file:
        # Write the numbers to the file
        for j in range(numbers_per_file):
            file.write(str(random.randint(0, 9)))

# Print a success message
print(
    f"Successfully created {num_files} files with {numbers_per_file} numbers each.")
