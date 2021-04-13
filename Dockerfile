FROM mcr.microsoft.com/azure-cli:latest

COPY acr-cleanup.sh /acr-cleanup.sh
RUN chmod +x /acr-cleanup.sh
ENTRYPOINT ["/bin/sh"]
CMD [ "/acr-cleanup.sh" ]