import xacro

# The input and output file names
input_file = "robotiq_2f_140_gripper.urdf_2.xacro"
output_file = "robotiq_2f_140_unity.urdf"

# Tell the xacro library to process the file
doc = xacro.process_file(input_file)

# Write the result to your new URDF file
with open(output_file, "w") as f:
    f.write(doc.toxml())

print("Success! Your URDF is ready for Unity.")