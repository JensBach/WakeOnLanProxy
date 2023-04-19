# WakeOnLanProxy
a WOL proxy in C#

<!-- USAGE EXAMPLES -->
## Usage

Start the program on a server on the same subnet as the WOL sender

change/add new destanations in the program code

If you are using the program in a Cisco network you can enable "directed-broadcast"


!
access-list 130 permit udp host [WakeOnLanProxy] any eq 40000
!
Interface vlan xxx
 ip directed-broadcast 130
!




<!-- CONTACT -->
## Contact

Jens Bach - Jensb82@gmail.com





