import os

with open('random_generated_file', 'wb') as fout:
    fout.write(os.urandom(307200))
