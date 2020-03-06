#!/bin/sh -e
### BEGIN INIT INFO
# Provides: KidsNoteForEveryone by KL
# Required-Start:
# Required-Stop:
# X-Start-Before:
# X-Stop-After:
# Default-Start: 2 3 4 5
# Default-Stop: 0 1 6
# Short-Description: KidsNoteForEveryone
# Description: KidsNoteForEveryone Daemon
### END INIT INFO

PATH="/sbin:/bin:/usr/bin"
RUN_DIR="/home/pi/knfe"

case "$1" in
start)
	cd $RUN_DIR
	mono-service -l:$RUN_DIR/service.lock $RUN_DIR/KidsNoteForEveryoneService.exe
	;;

stop)
	kill `cat $RUN_DIR/service.lock`
	rm $RUN_DIR/service.lock
	;;
*)
	cd $RUN_DIR
	mono-service -l:$RUN_DIR/service.lock $RUN_DIR/KidsNoteForEveryoneService.exe
	;;
esac

exit 0

# vim: noet ts=8
